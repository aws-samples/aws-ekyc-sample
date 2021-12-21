import React, {useState} from 'react';
import {ICreateSessionResponse} from "../EkycSession";
import Utils from "../../Utils";
import {API} from "aws-amplify";
import {Alert, Container} from "aws-northstar";
import Stack from "aws-northstar/layouts/Stack";
import Button from "aws-northstar/components/Button";
import Webcam from "react-webcam";
import LoadingIndicator from "aws-northstar/components/LoadingIndicator";

const apiName = "ekycApi"

export interface NosePointTakerProps {

    session: ICreateSessionResponse
    selfieImgSrc: string
    onNoisePointTaken: () => void
}


const videoConstraints = {
    width: 1280,
    height: 720,
    facingMode: "user"
};

function NosePointTaker(props: NosePointTakerProps) {

    let ctx: any;

    const height = 720

    const width = 1280

    const canvas = React.useRef(null);

    const webcamRef = React.useRef(null);

    const [previewState, setPreviewState] = useState(true)

    const [imgSrc, setImgSrc] = React.useState(null);

    const [isLoading, setIsLoading] = React.useState(false)

    const [error, setError] = React.useState<string>()

    const capture = React.useCallback(
        () => {
            const imageSrc = webcamRef.current.getScreenshot();
            setImgSrc(imageSrc)
            setPreviewState(false)

        },
        [webcamRef, setImgSrc]
    );

    const s3Key = 'nosepoint.jpg'

    // draw rectangle
    const drawRect = (info: any, style: any) => {
        const {x, y, w, h} = info;
        const {borderColor = 'red', borderWidth = 3} = style;

        ctx.beginPath();
        ctx.strokeStyle = borderColor;
        ctx.lineWidth = borderWidth;
        ctx.rect(x, y, w, h);
        ctx.stroke();
    }

    // initialize the canvas context
    React.useEffect(() => {
        // dynamically assign the width and height to canvas
        const canvasEle = canvas.current;
        canvasEle.width = canvasEle.clientWidth;
        canvasEle.height = canvasEle.clientHeight;

        // get context of the canvas
        ctx = canvasEle.getContext("2d");

        const r1Info = {
            x: props.session.noseBoundsLeft * width,
            y: props.session.noseBoundsTop * height,
            w: props.session.noseBoundsWidth * width,
            h: props.session.noseBoundsHeight * height
        };
        const r1Style = {borderColor: 'red', borderWidth: 3};
        drawRect(r1Info, r1Style);

    }, []);

    const retake = React.useCallback(() => {
        setPreviewState(true)
    }, [webcamRef])

    const uploadNosePoint = async () => {

        if (props && props.session.id) {
            setIsLoading(true)
            // Get the presigned URL
            const url = getPresignedUrl().then(
                (url) => {
                    console.log(`Presigned URL: ${url}`)

                    Utils.uploadFile(url, imgSrc, 'image/jpeg')
                        .then(() => {
                            API.post(apiName, '/api/session/nosepoint', {
                                queryStringParameters: {
                                    'sessionId': props.session.id,
                                    's3Key': s3Key
                                }
                            })
                        })
                        .then(() => {
                            setIsLoading(false)
                            props.onNoisePointTaken()

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
            )

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

            <Container headingVariant='h4' title="Take a Selfie"
                       subtitle="Please ensure your nose is pointing into the box.">
                <Stack spacing='xs'>
                    <div style={{display: error ? 'block' : 'none'}}>
                        <Alert type="error">{error}</Alert>
                    </div>
                    <div>
                        <canvas height={height} style={{zIndex: 1, position: "absolute"}} width={width}
                                ref={canvas}></canvas>
                    </div>
                    <div style={{display: previewState ? 'none' : 'block'}}>
                        <React.Fragment>
                            <Stack spacing='xs'>
                                <img
                                    src={imgSrc}
                                />
                                <Button variant="primary" onClick={retake}>Retake photo</Button>

                                <Button variant="primary" onClick={uploadNosePoint}>Use this Selfie</Button>
                            </Stack>
                        </React.Fragment>
                    </div>

                    <React.Fragment>
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
                            <div style={{display: isLoading ? 'none' : 'block'}}>
                                <Button variant="primary" onClick={capture}>Capture photo</Button>
                            </div>
                        </div>
                    </React.Fragment>
                    <React.Fragment>
                        <div style={{display: isLoading ? 'block' : 'none'}}>
                            <LoadingIndicator label='Uploading'/>
                        </div>
                    </React.Fragment>

                </Stack>

            </Container>

        </div>)
}

export default NosePointTaker

