import React, {useEffect, useState} from "react";
import Button from "aws-northstar/components/Button";
import {Alert, Container} from "aws-northstar";
import {API} from "aws-amplify";
import Select from "aws-northstar/components/Select";
import axios from "axios";
import LoadingIndicator from "aws-northstar/components/LoadingIndicator";
import Paper from "aws-northstar/layouts/Paper";
import Stack from "aws-northstar/layouts/Stack";
import Box from "aws-northstar/layouts/Box";
import Text from "aws-northstar/components/Text";
import JSONPretty from "react-json-pretty";

const apiName = "ekycApi";

interface DocumentType {
    Code: string;
    Name: string;
}

interface FieldDataRow {
    fieldName: string;
    value: string;
}

const videoConstraints = {
    width: 1280,
    height: 720,
    facingMode: "user",
};
const FieldData = () => {
    const [isLoading, setIsLoading] = useState(false);

    const [isFaceLoading, setIsFaceLoading] = useState(false);

    const [selectedDocumentType, setSelectedDocumentType] = React.useState(null);

    const [fieldData, setFieldData] = React.useState([]);

    const [fieldOutput, setFieldOutput] = React.useState(null);

    const [documentTypes, setDocumentTypes] = React.useState([]);

    const [options, setOptions] = React.useState([]);

    const [documentPreview, setDocumentPreview] = React.useState(null);

    const [imgData, setImgData] = useState(null);

    const [error, setError] = React.useState<string>();

    const [faceImage, setFaceImage] = React.useState(null);

    const [uploadedFile, setUploadedFile] = React.useState(null);

    const [requestId, setRequestId] = React.useState<string>();

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

            setIsLoading(false);
        }

        async function createRequest() {
            API.post(apiName, "/api/data/request/create", {}).then((response) => {
                console.log(`Data request created ${JSON.stringify(response)}`);
                setRequestId(response.requestId);
                setIsLoading(false);
            });
        }

        getDocumentTypes();
        createRequest();
    }, []);

    const useUploadedFile = (element: any) => {
        if (!selectedDocumentType) {
            setError("Document type not selected.");
            return;
        }

        if (uploadedFile === null) {
            setError("No file selected.");
            return;
        }

        setIsLoading(true);
        setIsFaceLoading(true);

        console.log(`Using file ${uploadedFile}`);

        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore
        const documentType = documentTypes.find(
            (o) => o.Code === selectedDocumentType.value
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
                    API.post(apiName, "/api/data/face", {
                        body: {
                            RequestId: requestId,
                            s3Key: s3Key,
                            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
                            // @ts-ignore
                            documentType: selectedDocumentType.value,
                        },
                        headers: {"Content-Type": "application/json"},
                    })
                        .then((response) => {
                            if (response.data) setFaceImage(response.data);
                            else setFaceImage(null);
                            setIsFaceLoading(false);
                        })
                        .catch((err) => {
                            setIsFaceLoading(false);
                            console.log(err.response.data.error);

                            if (err.response) setError(err.response.data.error);
                            else setError("An error occurred obtaining the face data.");
                            /*console.log(err.data.error)
                                              if (err.response)
                                                  setError(err.data.error)0
                                              else
                                                  setError('An error occurred getting field values.')
                                                  */
                        });

                    API.post(apiName, "/api/data/fields", {
                        body: {
                            RequestId: requestId,
                            s3Key: s3Key,
                            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
                            // @ts-ignore
                            documentType: selectedDocumentType.value,
                        },
                        headers: {"Content-Type": "application/json"},
                    })
                        .then((response) => {
                            setIsLoading(false);
                            setError(undefined);
                            console.log(
                                `Response from /data/fields: ${JSON.stringify(response)}`
                            );
                            setFieldOutput(response);
                        })
                        .catch((err) => {
                            setIsLoading(false);
                            console.log(err.response.data.error);
                            setFieldOutput(undefined);
                            if (err.response) setError(err.response.data.error);
                            else setError("An error occurred obtaining the field data.");
                            /*console.log(err.data.error)
                                          if (err.response)
                                              setError(err.data.error)0
                                          else
                                              setError('An error occurred getting field values.')
                                              */
                        });
                });
        });
    };

    const onChange = (event: any) =>
        setSelectedDocumentType(
            options.find((o) => o.value === event.target.value)
        );

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

    const getPresignedUrl = async () => {
        const response = await API.get(apiName, "/api/data/url", {
            queryStringParameters: {
                requestId: requestId,
                s3Key: s3Key,
            },
        });

        return response;
    };

    const columnDefinitions = [
        {
            id: "fieldName",
            width: 200,
            Header: "Field Name",
            accessor: "fieldName",
        },
        {
            id: "value",
            width: 300,
            Header: "Value",
            accessor: "value",
        },
    ];

    return (
        <div>
            <Container headingVariant="h3" title="Get Field Data from Documents">
                <div style={{display: error ? "block" : "none"}}>
                    <Alert type="error">{error}</Alert>
                </div>
                <Container headingVariant="h4" title="Choose the document type">
                    <div style={{display: isLoading ? "none" : "block"}}>
                        <Select
                            placeholder="Choose a document type"
                            options={options}
                            selectedOption={selectedDocumentType}
                            onChange={(e) => onChange(e)}
                            loadingText="Loading"
                        />
                    </div>
                </Container>

                <Container headingVariant="h4" title="Upload a Document">
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
                <div
                    style={{
                        display: isLoading || fieldOutput === null ? "none" : "block",
                    }}
                >
                    <Paper>
                        <Box p={1} width="100%">
                            <Stack spacing="xs">
                                <Text>
                                    <b>Field and Facial Data from Document</b>
                                </Text>
                                <JSONPretty id="json-pretty" data={fieldOutput}></JSONPretty>
                                <div
                                    style={{
                                        display:
                                            isFaceLoading || faceImage !== null ? "block" : "none",
                                    }}
                                >
                                    <img src={`data:image/png;base64,${faceImage}`}/>
                                </div>
                            </Stack>
                        </Box>
                    </Paper>
                </div>
            </Container>
        </div>
    );
};

export default FieldData;
