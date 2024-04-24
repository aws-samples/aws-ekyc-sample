import React, {useEffect, useState} from "react";

import {API} from "aws-amplify";

import axios from "axios";

import JSONPretty from "react-json-pretty";
import {Alert, Box, Button, Container, Header, Select, SpaceBetween, Spinner} from "@cloudscape-design/components";
import {SelectProps} from "@cloudscape-design/components/select/interfaces";
import {apiName} from "../../constants";


interface DocumentType {
    code: string;
    name: string;
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

    const [isLandmarksLoading, setIsLandmarksLoading] = useState(false);

    const [selectedDocumentType, setSelectedDocumentType] = React.useState<SelectProps.Option | null>(null);

    const [fieldOutput, setFieldOutput] = React.useState();

    const [documentTypes, setDocumentTypes] = React.useState<{
        name: string,
        code: string,
        faceExtractionSupported: boolean,
        livenessSupported: boolean
    }[]>([]);

    const [options, setOptions] = React.useState<SelectProps.Option[]>([]);

    const [documentPreview, setDocumentPreview] = React.useState(null);

    const [imgData, setImgData] = useState<any>(null);

    const [error, setError] = React.useState<string>();

    const [faceImage, setFaceImage] = React.useState(null);

    const [landmarksImage, setLandmarksImage] = React.useState(null);

    const [uploadedFile, setUploadedFile] = React.useState<any>(null);

    const [requestId, setRequestId] = React.useState<string>();

    const s3Key = "document.jpg";


    useEffect(() => {
        async function getDocumentTypes() {
            // Get the document types

            const docTypes = await API.get(apiName, "/api/document/doctypes", {});

            setDocumentTypes(docTypes);


            const selectOptions = docTypes.map((dt: DocumentType) => {
                return {label: dt.name, value: dt.code};
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
        setIsLandmarksLoading(true)
        setFaceImage(null)
        setFieldOutput(undefined)
        setLandmarksImage(null)

        console.log(`Using file ${uploadedFile}`);

        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore
        const documentType = documentTypes.find(
            (o) => o?.code === selectedDocumentType?.value
        );

        console.log(`Document type - ${documentType?.code}`);

        // call api here
        getPresignedUrl().then((url) => {
            axios
                .put(url, uploadedFile,
                    //     {
                    //     // headers: {
                    //     //     "Content-Type": uploadedFile.type,
                    //     // },
                    // }
                )
                .then(() => {

                    API.post(apiName, "/api/data/fields/full", {
                        body: {
                            RequestId: requestId,
                            s3Key: s3Key,
                            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
                            // @ts-ignore
                            documentType: selectedDocumentType.value,
                        },
                        headers: {"Content-Type": "application/json"},
                    }).then((response) => {
                        console.log(`Get field values response: ${JSON.stringify(response)}`)
                        setIsFaceLoading(false)
                        setIsLoading(false)
                        setIsLandmarksLoading(false)
                        if (response?.faces?.data)
                            setFaceImage(response?.faces?.data)
                        if (response?.landmarks?.data)
                            setLandmarksImage(response?.landmarks?.data)
                        setFieldOutput(response?.fieldValues)
                    })
                        .catch((err) => {
                            setIsFaceLoading(false)
                            setIsLoading(false)
                            setIsLandmarksLoading(false)
                            console.log(err)
                            setError(err)
                        })

                });
        });
    };

    const onChange = (newValue?: string) =>
        setSelectedDocumentType(
            options.find((o) => o.value === newValue) ?? null
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
        <SpaceBetween size={"s"}>
            <Header variant={"h2"}>Get Field Data from Documents</Header>
            <div style={{display: error ? "block" : "none"}}>
                <Alert type="error">{error}</Alert>
            </div>
            <Header variant={"h3"}>Choose the document type</Header>
            <div style={{display: isLoading ? "none" : "block"}}>
                <Select
                    placeholder="Choose a document type"
                    options={options}
                    selectedOption={selectedDocumentType}
                    onChange={(e) => onChange(e.detail.selectedOption?.value)}
                    loadingText="Loading"
                />
            </div>


            <SpaceBetween size="m">
                <Header variant={"h3"}>Upload a Document</Header>
                <input
                    type="file"
                    accept="image/jpeg,image/png"
                    onChange={onFileUploadChanged}
                />
                <div style={{display: imgData !== null ? "block" : "none"}}>
                    <img width={1280} src={imgData ?? ""} alt={""}/>
                </div>
                {!isLoading && !isFaceLoading &&
                    <div style={{display: uploadedFile !== null ? "block" : "none"}}>
                        <Button variant="primary" onClick={useUploadedFile}>
                            Use File
                        </Button>
                    </div>
                }
            </SpaceBetween>

            {isLoading &&
                <div>
                    <Spinner/>
                </div>
            }
            {(!isLoading && fieldOutput) &&

                <div>

                    <Container>
                        <Box padding={"m"}>
                            <SpaceBetween size={"xs"}>

                                <b>Field and Facial Data from Document</b>

                                <JSONPretty id="json-pretty" data={fieldOutput}></JSONPretty>
                                {!isFaceLoading && faceImage &&
                                    <div>
                                        <img src={`data:image/png;base64,${faceImage}`} alt={""}/>
                                    </div>
                                }
                                {!isLandmarksLoading && landmarksImage &&
                                    <div>
                                        <img width={1280} src={`data:image/png;base64,${landmarksImage}`} alt={""}/>
                                    </div>

                                }
                            </SpaceBetween>
                        </Box>
                    </Container>
                </div>
            }

        </SpaceBetween>
    );
};

export default FieldData;
