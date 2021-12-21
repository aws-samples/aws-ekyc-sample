import React, {useEffect, useState} from "react";
import Button from "aws-northstar/components/Button";
import {Alert, Container} from "aws-northstar";
import Webcam from "react-webcam";
import Stack from "aws-northstar/layouts/Stack";
import {API} from "aws-amplify";
import Select from "aws-northstar/components/Select";
import Utils from "../../Utils";
import axios from "axios";
import LoadingIndicator from "aws-northstar/components/LoadingIndicator";

const apiName = "ekycApi";

export interface DocumentTakerProps {
    sessionId?: string;
    onDocumentImageTaken: (documentType: string) => void;
}

interface DocumentType {
    Code: string;
    Name: string;
}

const videoConstraints = {
    width: 1280,
    height: 720,
    facingMode: "user",
};
const DocumentTaker = (props: DocumentTakerProps) => {
    const webcamRef = React.useRef(null);

    const [previewState, setPreviewState] = useState(true);

    const [imgSrc, setImgSrc] = React.useState(null);

    const [isLoading, setIsLoading] = useState(false);

    const [selectedOption, setSelectedOption] = React.useState(null);
    //const [documentType,setDocumentType] = useState<SelectOption>()

    const [documentTypes, setDocumentTypes] = React.useState([]);

    const [imgData, setImgData] = useState(null);

    const [options, setOptions] = React.useState([]);

    const [error, setError] = React.useState<string>();

    const [documentPreview, setDocumentPreview] = useState(null);

    const [uploadedFile, setUploadedFile] = React.useState(null);

    const s3Key = "document.jpg";

    useEffect(() => {
        async function getDocumentTypes() {
            // Get the document types

            const docTypes = await API.get(apiName, "/api/document/doctypes", {});

            setDocumentTypes(docTypes);

            console.log(docTypes);

            const selectOptions = docTypes.map((dt: DocumentType) => {
                return {label: dt.Name, value: dt.Code};
            });

            console.log(`Document type options - ${JSON.stringify(selectOptions)}`);

            setOptions(selectOptions);
        }

        getDocumentTypes();
    }, []);

    const useUploadedFile = (element: any) => {
        if (!selectedOption) {
            setError("Document type not selected.");
            return;
        }

        if (uploadedFile === null) {
            setError("No file selected.");
            return;
        }

        setIsLoading(true);

        console.log(`Using file ${uploadedFile}`);

        const documentType = documentTypes.find(
            (o) => o.Code === selectedOption.value
        );

        console.log(`Document type - ${documentType.Code}`);

        // call api here
        getPresignedUrl().then((url) => {
            axios
                .put(url, uploadedFile, {
                    headers: {
                        "Content-Type": uploadedFile.type,
                    },
                })
                .then(() => {
                    API.post(apiName, "/api/session/document", {
                        queryStringParameters: {
                            sessionId: props.sessionId,
                            s3Key: s3Key,
                            expectedDocumentType: documentType.Code,
                        },
                    })
                        .then(() => {
                            setIsLoading(false);
                            props.onDocumentImageTaken(documentType.Code);
                        })
                        .catch((err) => {
                            console.log(err.response.data.error);
                            if (err.response) setError(err.response.data.error);
                            else setError("An error occurred uploading the selfie.");

                            retake();
                        });
                });
        });
    };

    const capture = React.useCallback(() => {
        const imageSrc = webcamRef.current.getScreenshot();
        setImgSrc(imageSrc);
        setPreviewState(false);
        setError(null);
    }, [webcamRef, setImgSrc]);

    const retake = React.useCallback(() => {
        setPreviewState(true);
        setError(null);
        setIsLoading(false);
    }, [webcamRef]);

    const uploadDocument = async () => {
        if (!selectedOption) {
            setError("Document type not selected.");
            return;
        }
        setIsLoading(true);

        const documentType = documentTypes.find(
            (o) => o.Code === selectedOption.value
        );

        console.log(`Document type - ${documentType.Code}`);

        // Upload
        getPresignedUrl().then((url) => {
            Utils.uploadFile(url, imgSrc, "image/*").then(() => {
                API.post(apiName, "/api/session/document", {
                    queryStringParameters: {
                        sessionId: props.sessionId,
                        s3Key: s3Key,
                        expectedDocumentType: documentType.Code,
                    },
                }).then(() => {
                    setIsLoading(false);
                    props.onDocumentImageTaken(documentType.Code);
                });
            });
        });
    };

    const onFileUploadChanged = (event: any) => {
        if (event.target.files[0]) {
            setUploadedFile(event.target.files[0]);
            console.log("picture: ", event.target.files);
            setDocumentPreview(event.target.files[0]);
            const reader = new FileReader();
            reader.addEventListener("load", () => {
                setImgData(reader.result);
            });
            reader.readAsDataURL(event.target.files[0]);
        }
    };

    const onChange = (event: any) =>
        setSelectedOption(options.find((o) => o.value === event.target.value));

    const onChangeDocumentType = React.useCallback(
        (event: any) => {
            const ssOption = options.find((o) => o.value === event.target.value);
            console.log(`Setting document type to ${event.target.value}`);
            setSelectedOption(ssOption);
        },
        [selectedOption]
    );

    const getPresignedUrl = async () => {
        const response = await API.get(apiName, "/api/session/url", {
            queryStringParameters: {
                sessionId: props.sessionId,
                s3Key: s3Key,
            },
        });

        return response;
    };

    return (
        <div>
            <Container headingVariant="h4" title="Choose the document type">
                <div>
                    <Select
                        placeholder="Choose a document type"
                        options={options}
                        selectedOption={selectedOption}
                        onChange={onChange}
                        loadingText="Loading"
                    />
                </div>
            </Container>
            <Container headingVariant="h4" title="Take a Photo of the Document">
                <Stack spacing="s">
                    <div style={{display: error ? "block" : "none"}}>
                        <Alert type="error">{error}</Alert>
                    </div>

                    <div style={{display: previewState ? "none" : "block"}}>
                        <React.Fragment>
                            <img src={imgSrc}/>
                            <Stack spacing="s">
                                <Button variant="primary" onClick={retake}>
                                    Retake photo
                                </Button>

                                <Button variant="primary" onClick={uploadDocument}>
                                    Use this
                                </Button>
                            </Stack>
                        </React.Fragment>
                    </div>
                    <React.Fragment>
                        <div style={{display: previewState ? "block" : "none"}}>
                            <Stack spacing="s">
                                <Webcam
                                    audio={false}
                                    height={720}
                                    ref={webcamRef}
                                    screenshotFormat="image/jpeg"
                                    width={1280}
                                    videoConstraints={videoConstraints}
                                />
                                <Button variant="primary" onClick={capture}>
                                    Capture photo
                                </Button>
                            </Stack>
                        </div>
                    </React.Fragment>
                </Stack>
            </Container>

            <Container headingVariant="h4" title="Or Upload a Document">
                <Stack spacing="s">
                    <input
                        type="file"
                        accept="image/jpeg,image/png"
                        onChange={onFileUploadChanged}
                    />
                    <div style={{display: imgData !== null ? "block" : "none"}}>
                        <img width={1280} src={imgData}/>
                    </div>
                    <div style={{display: uploadedFile !== null ? "block" : "none"}}>
                        <Button variant="primary" onClick={useUploadedFile}>
                            Use File
                        </Button>
                    </div>
                </Stack>
            </Container>

            <div style={{display: isLoading ? "block" : "none"}}>
                <LoadingIndicator label="Uploading"/>
            </div>
        </div>
    );
};

export default DocumentTaker;
