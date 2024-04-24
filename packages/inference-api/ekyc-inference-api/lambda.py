import json
import os

import boto3
import cv2
from PIL import Image
from flask import jsonify
from pytesseract import pytesseract

from thai_id import extract_thai_id_front_info, extract_thai_id_back_info, card_to_dict

bucket_name = os.environ("StorageBucket")


def lambda_handler(event, context):
    print(f'Event: {event}')
    print(f'Context: {context}')

    s3Key = event.get('s3Key')
    mode = event.get('mode')

    # Download the file from S3

    fileName = f'/tmp/{s3Key}'

    s3 = boto3.client('s3')
    s3.download_file(bucket_name, s3Key, fileName)

    image = cv2.imread(fileName)

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # apply thresholding to preprocess the image
    gray = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)[1]

    # apply median blurring to remove any blurring
    gray = cv2.medianBlur(gray, 3)

    # save the processed image in the /static/uploads directory

    ofilename = os.path.join('/tmp', "{}.png".format(os.getpid()))
    cv2.imwrite(ofilename, gray)

    if mode == 'FRONT':
        response = extract_thai_id_front_info(ofilename)
    elif mode == 'BACK':
        response = extract_thai_id_back_info(ofilename)
    else:
        text = pytesseract.image_to_string(Image.open(ofilename), lang="th", timeout=10)
        text = text.replace(" ", "")
        text = text.replace("\n", "")
        print(f"Text output: {text}")
        return {'statusCode': 200,
                'body':
                    json.dumps({"result": text})}

    return {
        'statusCode': 200,
        'body': json.dumps(jsonify(card_to_dict(response)))
    }
