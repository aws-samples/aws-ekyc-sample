import React, {useState} from 'react';
import Button from 'aws-northstar/components/Button';
import {Alert, Container} from "aws-northstar";
import Webcam from "react-webcam";
import Stack from 'aws-northstar/layouts/Stack';
import {API} from "aws-amplify";
import Utils from "../../Utils";
import LoadingIndicator from 'aws-northstar/components/LoadingIndicator';
import Text from 'aws-northstar/components/Text';

const apiName = "ekycApi"
const fileName = 'selfie.jpg'
const height = 720
const width = 1280

export interface SelfieTakerProps {
    sessionId?: string
    onSelfieTaken: (imgSrc: string) => void
}

const videoConstraints = {
    width: 1280,
    height: 720,
    facingMode: "user"
};
const SelfieTaker = (props: SelfieTakerProps) => {


    const webcamRef = React.useRef(null);

    let ctx: any;


    const [previewState, setPreviewState] = useState(true)

    const [isLoading, setIsLoading] = useState(false)

    const [imgSrc, setImgSrc] = React.useState(null);

    const canvas = React.useRef(null);

    const [error, setError] = React.useState<string>()

    const capture = React.useCallback(
        () => {
            const imageSrc = webcamRef.current.getScreenshot();
            setImgSrc(imageSrc)
            setPreviewState(false)

        },
        [webcamRef, setImgSrc, previewState]
    )

    // initialize the canvas context
    React.useEffect(() => {
        // dynamically assign the width and height to canvas
        const canvasEle = canvas.current;
        canvasEle.width = canvasEle.clientWidth;
        canvasEle.height = canvasEle.clientHeight;

        // get context of the canvas
        ctx = canvasEle.getContext("2d");

        ctx.beginPath();
        ctx.moveTo(width / 2, 0);
        ctx.lineTo(width / 2, height)
        ctx.strokeStyle = "red";
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(0, height / 2);
        ctx.lineTo(width, height / 2);
        ctx.strokeStyle = "red";
        ctx.stroke();

    }, []);

    const retake = React.useCallback(() => {
        setPreviewState(true)
        setIsLoading(false)
    }, [webcamRef])

    const uploadSelfie = async () => {

        setIsLoading(true)

        if (props && props.sessionId) {
            // Get the presigned URL
            const url = await getPresignedUrl()

            console.log(`Presigned URL: ${url}`)

            //  const actualBase64 = await Utils.stripBase64Chars(imgSrc)

            await Utils.uploadFile(url, imgSrc, 'image/jpeg')

            await API.post(apiName, '/api/session/selfie', {
                queryStringParameters: {
                    'sessionId': props.sessionId,
                    's3Key': fileName
                }
            })
                .then((response) => {
                    setIsLoading(false)
                    props.onSelfieTaken(imgSrc)

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


        }

    }

    const getPresignedUrl = async () => {

        if (props && props.sessionId) {
            const response = await (API.get(apiName, '/api/session/url', {
                queryStringParameters: {
                    'sessionId': props.sessionId,
                    's3Key': fileName
                }
            }))
            return response

        }
    }

    return (

        <div>

            <Container headingVariant='h4' title="Take a Selfie">
                <Stack spacing='xs'>
                    <Container headingVariant='h4'
                               title="Instructions"
                    >
                        <Text>Align your face with your nose in the middle of the red lines. Do not tilt or rotate your
                            head, and look straight on into the camera.</Text>
                    </Container>

                    <div style={{display: error ? 'block' : 'none'}}>
                        <Alert type="error">{error}</Alert>
                    </div>

                    <React.Fragment>
                        <Stack spacing='xs'>
                            <div>
                                <canvas height={height} style={{zIndex: 1, position: "absolute"}} width={width}
                                        ref={canvas}></canvas>
                            </div>
                            <div style={{display: previewState ? 'block' : 'none'}}>
                                <Webcam
                                    style={{zIndex: 0}}
                                    audio={false}
                                    height={height}
                                    ref={webcamRef}
                                    screenshotFormat="image/jpeg"
                                    width={width}
                                    videoConstraints={videoConstraints}
                                />
                            </div>

                            <div style={{display: previewState ? 'block' : 'none'}}>
                                <Button variant="primary" onClick={capture}>Capture photo</Button>
                            </div>


                        </Stack>
                    </React.Fragment>

                    <React.Fragment>
                        <div style={{display: previewState ? 'none' : 'block'}}>
                            <React.Fragment>
                                <Stack spacing='xs'>
                                    <img src={imgSrc}/>
                                    <div style={{display: isLoading ? 'none' : 'block'}}>
                                        <Stack spacing='xs'>
                                            <Button variant="primary" onClick={retake}>Retake photo</Button>
                                            <Button variant="primary" onClick={uploadSelfie}>Use this Selfie</Button>
                                        </Stack>

                                    </div>
                                    <div style={{display: isLoading ? 'block' : 'none'}}>
                                        <LoadingIndicator label='Uploading'/>
                                    </div>
                                </Stack>
                            </React.Fragment>
                        </div>
                    </React.Fragment>
                </Stack>

            </Container>

        </div>)
}

export default SelfieTaker
