import React, {useState} from 'react';
import Button from 'aws-northstar/components/Button';
import {Alert, Container} from "aws-northstar";
import Webcam from "react-webcam";
import Stack from 'aws-northstar/layouts/Stack';
import {API} from "aws-amplify";
import Utils from "../../Utils";
import {ICreateSessionResponse} from "../EkycSession";
import LoadingIndicator from "aws-northstar/components/LoadingIndicator";

const apiName = "ekycApi"

const s3Key = 'eyesclosed.jpg'

export interface EyesClosedTakerProps {
    session: ICreateSessionResponse
    onEyesCloseTaken: () => void
}

const videoConstraints = {
    width: 1280,
    height: 720,
    facingMode: "user"
};
const EyesClosedTaker = (props: EyesClosedTakerProps) => {

    const webcamRef = React.useRef(null);

    const [previewState, setPreviewState] = useState(true)

    const [imgSrc, setImgSrc] = React.useState(null);

    const [isLoading, setIsLoading] = React.useState(false)

    const [error, setError] = React.useState<string>(null)

    const capture = React.useCallback(
        () => {
            const imageSrc = webcamRef.current.getScreenshot();
            setImgSrc(imageSrc)
            setPreviewState(false)

        },
        [webcamRef, setImgSrc]
    );

    const retake = React.useCallback(() => {
        setPreviewState(true)
    }, [webcamRef])

    const uploadSelfie = async () => {

        if (props && props.session.id) {
            setIsLoading(true)

            getPresignedUrl().then(url => {
                console.log(`Presigned URL: ${url}`)

                Utils.uploadFile(url, imgSrc, 'image/jpeg')
                    .then(() => {
                        API.post(apiName, '/api/session/eyesclosed', {
                            queryStringParameters: {
                                'sessionId': props.session.id,
                                's3Key': s3Key
                            }
                        })
                    })
                    .then(() => {
                        setIsLoading(false)

                        props.onEyesCloseTaken()
                    })
                    .catch(err => {
                        setIsLoading(false)
                        console.log(err.response.data.error)
                        if (err.response)
                            setError(err.response.data.error)
                        else
                            setError('An error occurred uploading the selfie.')

                        retake()
                    })


            })

        }

    }

    const getPresignedUrl = async () => {

        if (props && props.session.id) {
            const response = await API.get(apiName, '/api/session/url', {
                queryStringParameters: {
                    'sessionId': props.session.id,
                    's3Key': s3Key
                }
            })
            return response

        }
    }

    return (

        <div>

            <Container headingVariant='h4' title="Please close your eyes and take a selfie">
                <Stack spacing='xs'>
                    <div style={{display: error ? 'block' : 'none'}}>
                        <Alert type="error">{error}</Alert>
                    </div>
                    <div style={{display: previewState ? 'none' : 'block'}}>
                        <React.Fragment>
                            <Stack spacing='xs'>
                                <img
                                    src={imgSrc}
                                />
                                <Button variant="primary" onClick={retake}>Retake photo</Button>

                                <Button variant="primary" onClick={uploadSelfie}>Use this Selfie</Button>
                            </Stack>
                        </React.Fragment>
                    </div>
                    <React.Fragment>
                        <div style={{display: previewState ? 'block' : 'none'}}>
                            <Webcam
                                audio={false}
                                height={720}
                                ref={webcamRef}
                                screenshotFormat="image/jpeg"
                                width={1280}
                                videoConstraints={videoConstraints}
                            />
                            <div>
                                <Button variant="primary" onClick={capture}>Capture photo</Button>
                            </div>
                        </div>

                    </React.Fragment>

                    <div style={{display: isLoading ? 'block' : 'none'}}>
                        <LoadingIndicator label='Uploading'/>
                    </div>

                </Stack>

            </Container>

        </div>)
}

export default EyesClosedTaker
