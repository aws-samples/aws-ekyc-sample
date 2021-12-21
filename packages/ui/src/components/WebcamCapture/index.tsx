import React, {useCallback, useRef, useState} from 'react';
import Webcam from "react-webcam"

export interface WebCamCaptureProps {
    onImageSelected: React.MouseEventHandler<HTMLButtonElement>
}

const videoConstraints = {
    width: 500,
    height: 500,
    facingMode: "user"

}

const WebcamComponent = () => <Webcam></Webcam>;

const WebcamCapture = (props: WebCamCaptureProps) => {

    const [image, setImage] = useState(null)

    const webcamRef = useRef(null);

    const capture = useCallback(() => {
        if (webcamRef && webcamRef.current) {
            const imageSrc = webcamRef.current.getScreenshot();
            setImage(imageSrc);
        }
    }, [webcamRef]);

    const useImage = (e: any) => {
        props.onImageSelected(e)
    }

    return (

        <div className="webcam-container">
            <Webcam
                audio={false}
                height={500}
                ref={webcamRef}
                screenshotFormat="image/jpeg"
                width={500}
                videoConstraints={videoConstraints}/>
            <span>
        <button onClick={capture}>Capture photo</button>
  </span>
            {image != '' &&
            <><img src={image}/>
                <div>
                    <button onClick={useImage}>Use this image</button>
                </div>
            </>
            }
        </div>
    );
};

export default WebcamCapture
