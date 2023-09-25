import React, {FC, useState} from "react";
import {Alert, Box, Button, Container, SpaceBetween, Spinner, TextContent} from "@cloudscape-design/components";
import {useGetDocumentTypes} from "../../hooks/FieldData";
import {FileToUpload, useCreateJobAndUpload} from "../../hooks/Training";
import {v4 as uuid} from 'uuid';

export const StartTraining: FC = () => {


    const [error, setError] = useState("")
    const [msg, setMsg] = useState("")

    const {
        isLoading: isLoadingDocumentTypes,
        isSuccess: isSuccessDocumentTypes,
        isError: isErrorDocumentTypes,
        data: dataDocumentTypes
    } = useGetDocumentTypes()
    const [files, setFiles] = useState<FileToUpload[]>([])
    const {
        mutate: createJobAndUpload,
        data: createdJobArn,
        isError: isErrorCreateJobAndUpload,
        isSuccess: isSuccessCreateJobAndUpload,
        isLoading: isLoadingCreateJobAndUpload
    } = useCreateJobAndUpload()

    if (isLoadingDocumentTypes)
        return <div><Spinner></Spinner>Loading document types...</div>

    const uploadFiles = async () => {
        setError("")
        if (files && files.length > 0) {

            createJobAndUpload({files})


        } else
            setError('No files to upload.')

    }


    const onFileUploadChanged = (event: any) => {

        console.log(JSON.stringify(event.target.files))
        if (event.target.files) {
            Array.from(event.target.files).forEach((file: any) => {
                console.log(`Pushing file ${JSON.stringify(file)}`)
                const reader = new FileReader();
                reader.addEventListener("load", () => {
                    files.push({name: `${uuid()}.jpg`, data: reader.result})
                });
                reader.readAsDataURL(file);
            })
        }

    }
    const docTypeList = () => {
        return (<>
            {
                dataDocumentTypes?.map(docType => {
                    return (<Box key={`box${docType.name}`}>- {docType.name}</Box>)
                })
            }

        </>)
    }

    return (
        <Container>
            <SpaceBetween size={"m"}>
                {isSuccessCreateJobAndUpload && createdJobArn &&
                    <Alert type={"info"}>Sagemaker labelling job {createdJobArn} has been created.</Alert>
                }
                {(error || isErrorDocumentTypes) &&
                    <Alert type="error">{error}</Alert>
                }
                {msg &&
                    <Alert type="success">{msg}</Alert>
                }

                <TextContent>Training jobs help to train the Rekognition Custom Labels model that can help with removal
                    of
                    background clutter in a document image. To train a model, you should have a collection of each
                    type of document, with a variety of backgrounds.</TextContent>

                {isSuccessDocumentTypes &&
                    <SpaceBetween size={"xs"}>

                        {dataDocumentTypes.length > 0 &&
                            <>
                                <TextContent><b>Here are the document types currently supported: </b></TextContent>
                                {docTypeList()}
                            </>
                        }


                        <TextContent>Start by uploading a series of images. For each document type, you should aim to
                            include at least 10 images, with a variety of backgrounds.</TextContent>

                    </SpaceBetween>
                }

                <input
                    type="file"
                    accept="image/jpeg,image/png"
                    onChange={onFileUploadChanged}
                    multiple
                />

                {!isLoadingCreateJobAndUpload &&
                    <Button variant="primary" onClick={uploadFiles}>
                        Upload Files
                    </Button>
                }
                {isLoadingCreateJobAndUpload &&
                    <><Spinner></Spinner>
                        <div>Uploading files and starting</div>

                    </>
                }
            </SpaceBetween>
        </Container>)
}