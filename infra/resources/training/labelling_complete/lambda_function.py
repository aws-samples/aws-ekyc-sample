import argparse
import copy
import io
import json
import os
import time
from urllib.parse import urlparse

import albumentations as A
import boto3
import cv2
import numpy as np
from PIL import ImageDraw, ImageFont, Image
from loguru import logger

AUGMENTATIONS_PER_IMAGE = 50

sm_client = boto3.client("sagemaker")
s3_client = boto3.client("s3")
rek_client = boto3.client("rekognition")
ddb_client = boto3.client('dynamodb')
asset_bucket = os.environ.get("STORAGE_BUCKET")

is_debug = False


class FullS3Url(object):

    def __init__(self, url):
        self._parsed = urlparse(url, allow_fragments=False)

    @property
    def bucket(self):
        return self._parsed.netloc

    @property
    def key(self):
        if self._parsed.query:
            return self._parsed.path.lstrip("/") + "?" + self._parsed.query
        else:
            return self._parsed.path.lstrip("/")

    @property
    def url(self):
        return self._parsed.geturl()


def create_rekognition_project(project_name):
    logger.debug(f'Project name: {project_name}')
    create_response = rek_client.create_project(ProjectName=project_name)
    arn = create_response["ProjectArn"]
    logger.debug(f'Created Rekognition project {arn}')
    return arn


def update_job_states():
    # TODO
    logger.debug('Write the job states to DDB')


def display_image(np_image, bbox, class_id, caption="Transformed"):
    # Ready image to draw bounding boxes on it.
    image = Image.fromarray(np_image)
    draw = ImageDraw.Draw(image)

    left = round(bbox[0])
    top = round(bbox[1])
    width = round(bbox[2])
    height = round(bbox[3])

    fnt = ImageFont.truetype('/Library/Fonts/Arial.ttf', 50)
    draw.text((left, top), str(class_id), fill='#00d400', font=fnt)

    points = (
        (left, top),
        (left + width, top),
        (left + width, top + height),
        (left, top + height),
        (left, top))
    draw.line(points, fill='#00d400', width=5)

    # image.show()

    cv2.imshow(caption, np.array(image))
    cv2.waitKey(0)  # waits until a key is pressed
    cv2.destroyAllWindows()  # destroys the window showing image


def perform_augmentations(manifestPath):
    s = FullS3Url(manifestPath)

    # Declare an augmentation pipeline
    transform = A.Compose([
        A.RandomRotate90(),
        A.ChannelShuffle(),
        # A.HorizontalFlip(p=0.5),
        # A.VerticalFlip(p=0.5),
        A.ColorJitter(),
        A.ToSepia(),
        A.SafeRotate(),
        A.ToGray(),
        A.RandomBrightnessContrast(p=0.5),
    ], bbox_params=A.BboxParams(format='coco'))

    # Download the manifest
    get_manifest_response = s3_client.get_object(Bucket=s.bucket, Key=s.key)

    # manifest_str = get_manifest_response['Body'].read().decode('utf-8')
    # manifest_lines = str.split(manifest_str, '\n')
    manifest_lines = get_manifest_response['Body'].iter_lines()

    manifest_without_filename = s.key[:s.key.rindex('/')]

    logger.debug(f'Manifest without filename: {manifest_without_filename}')

    data = []
    final_manifest_data = []
    for line in manifest_lines:
        logger.debug(f'Manifest line: {line}')
        data.append(json.loads(line))
        final_manifest_data.append(json.loads(line))

    for labelled_file in data:
        bucket, key = labelled_file["source-ref"].split('/', 2)[-1].split('/', 1)
        labelled_data_response = s3_client.get_object(Bucket=bucket, Key=key)
        labelled_data_image = labelled_data_response["Body"].read()

        print('Augmenting ', labelled_file["source-ref"])

        bboxes = labelled_file["your-label-attribute"]["annotations"]

        # Bounding boxes in COCO format
        coco_bbox = []

        np_array = np.frombuffer(labelled_data_image, np.uint8)

        # Decode the image using OpenCV
        source_image = cv2.imdecode(np_array, cv2.IMREAD_COLOR)

        for bbox in bboxes:
            coco_bbox.append([bbox["left"], bbox["top"], bbox["width"], bbox["height"],
                              str(bbox["class_id"])])
            left = round(bbox["left"])
            top = round(bbox["top"])
            width = round(bbox["width"])
            height = round(bbox["height"])
            if is_debug:
                display_image(source_image, [left, top, width, height], str(bbox["class_id"]), "Original")

        logger.debug(f'Coco bbox: {coco_bbox}')

        filename_only = key[key.rindex('/') + 1:]
        before_filename = key[:key.rindex('/')]

        logger.debug(f'filename only: {filename_only}')
        logger.debug(f'before_filename: {before_filename}')

        for x in range(AUGMENTATIONS_PER_IMAGE):
            transformed = transform(image=source_image, bboxes=coco_bbox)
            transformed_image = transformed["image"]

            transformed_bboxes = transformed['bboxes']

            is_success, buffer = cv2.imencode(".png", transformed_image)
            io_buf = io.BytesIO(buffer)

            transformed_annotations = []

            for bbox in transformed_bboxes:
                left = round(bbox[0])
                top = round(bbox[1])
                width = round(bbox[2])
                height = round(bbox[3])
                transformed_annotations.append(
                    {"class_id": int(bbox[4]),
                     "left": left,
                     "top": top,
                     "width": width,
                     "height": height})
                if is_debug:
                    display_image(transformed_image, bbox, bbox[4])

            logger.debug(f'Transformed annotations: {transformed_annotations}')

            new_filename = f'{filename_only[:filename_only.rindex(".")]}_transformed_{x}.png'

            new_s3_key = f'{before_filename}/augmented/{new_filename}'

            # put the object in S3
            s3_client.put_object(Body=io_buf.read(), Bucket=bucket, Key=new_s3_key)

            logger.debug(f'Put to S3: {new_s3_key}')

            new_file = copy.copy(labelled_file)

            new_file["source-ref"] = f"s3://{bucket}/{new_s3_key}"

            new_file["your-label-attribute"]["annotations"] = transformed_annotations

            # remove existing "image_size" field
            new_file["your-label-attribute"].pop("image_size", None)

            new_file["your-label-attribute"]["image_size"] = [
                {"width": transformed_image.shape[1], "height": transformed_image.shape[0], 'depth': 3}]

            final_manifest_data.append(new_file)

    manifest_strings = []

    for man_data in final_manifest_data:
        manifest_strings.append(json.dumps(man_data))

    final_str = '\n'.join(manifest_strings)

    new_manifest_path = f'{manifest_without_filename}/augmented/output.manifest'

    with open("/tmp/augmented.manifest", "w") as f:
        f.write(final_str)

    s3_client.put_object(Bucket=s.bucket, Key=new_manifest_path, Body=final_str)

    return new_manifest_path


def handle_job(labelling_job_arn):
    job_name = labelling_job_arn[(labelling_job_arn.rfind("/") + 1):]
    labelling_job = sm_client.describe_labeling_job(LabelingJobName=job_name)

    if not labelling_job:
        logger.warning(f'Cannot find labelling job {job_name}')
        return

    s3_path = labelling_job["OutputConfig"]["S3OutputPath"]
    logger.debug(f'Job name {job_name}')
    logger.debug(f'S3 path {s3_path}')

    output_manifest_path = f'{s3_path}/{job_name}/manifests/output/output.manifest'

    logger.debug(f'Output manifest path {output_manifest_path}')

    new_manifest_path = perform_augmentations(output_manifest_path)

    logger.debug(f'New output manifest path {new_manifest_path}')

    rek_project_arn = create_rekognition_project(f"bb-{round(time.time())}")

    project_version_arn = create_project_version(rek_project_arn, new_manifest_path)

    logger.debug(f'Created Rekognition project version {project_version_arn}')


def create_project_version(projectArn, output_manifest_path):
    version_name = f'v{round(time.time())}'
    create_response = rek_client.create_project_version(
        ProjectArn=projectArn,
        VersionName=version_name,
        OutputConfig={
            'S3Bucket': asset_bucket,
            'S3KeyPrefix': f'{version_name}'
        },
        TrainingData={
            'Assets': [
                {
                    'GroundTruthManifest': {
                        'S3Object': {
                            'Bucket': asset_bucket,
                            'Name': output_manifest_path,
                        }
                    }
                },
            ]
        },
        TestingData={
            'AutoCreate': True
        }
    )

    proj_version_arn = create_response["ProjectVersionArn"]

    logger.debug(f'Created project version {proj_version_arn}')

    return proj_version_arn


def handler(event, context):
    logger.debug('Labelling job complete lambda invoked by EventBridge')
    logger.debug(f'Event: {event}')
    logger.debug(f'Context: {context}')

    if len(event["resources"]) == 0:
        logger.warning('No labelling job specified.')

    if event["detail"]["LabelingJobStatus"] != "Completed":
        logger.warning("Job is not complete, exiting.")
        return

    labelling_job_arn = event["resources"][0]
    handle_job(labelling_job_arn)

    # Example:
    # {
    #     "version": "0",
    #     "id": "4cfc5cfb-9afb-5a02-c8f2-e3ac95413a88",
    #     "detail-type": "SageMaker Ground Truth Labeling Job State Change",
    #     "source": "aws.sagemaker",
    #     "account": "213182973438",
    #     "time": "2023-07-17T14:58:57Z",
    #     "region": "ap-southeast-2",
    #     "resources": [
    #         "arn:aws:sagemaker:ap-southeast-2:213182973438:labeling-job/brand-compliance-6caf275b-392e-4293-b456-8176783c18d0"
    #     ],
    #     "detail": {
    #         "LabelingJobStatus": "Completed"
    #     }
    # }

    # Perform augmentation


if __name__ == "__main__":
    # Manual testing
    parser = argparse.ArgumentParser()
    parser.add_argument('--labelling_job', type=str, required=True)
    args = parser.parse_args()
    is_debug = True
    handle_job(args.labelling_job)
