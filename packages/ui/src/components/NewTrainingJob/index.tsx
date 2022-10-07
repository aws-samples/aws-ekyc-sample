// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import React, {useEffect, useState} from 'react';
import {Alert, Container} from "aws-northstar";
import Text from 'aws-northstar/components/Text';
import Stack from "aws-northstar/layouts/Stack";
import {API} from "aws-amplify";
import Utils from "../../Utils";
import Button from "aws-northstar/components/Button";
import LoadingIndicator from "aws-northstar/components/LoadingIndicator";
import {v4 as uuid} from 'uuid';

const apiName = 'ekycApi'

interface DocType {
    Name: string
    Code: string
}

interface FileToUpload {
    data: any
    name: string
}

function NewTrainingJob() {

    const [isLoading, setIsLoading] = useState(true)
    const [status, setStatus] = useState(null)
    const [files, setFiles] = useState<FileToUpload[]>([])
    const [error, setError] = useState(null)
    const [msg, setMsg] = useState(null)
    const [fileCount, setFileCount] = useState<number>(0)
    const [docTypes, setDocTypes] = useState(null)


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

    useEffect(() => {
        const fetchDocumentTypes = async () => {
            const response = await API.get(apiName, '/api/document/doctypes', {})

            setDocTypes(response.map((a: DocType) => a.Name).join(', '))
            setIsLoading(false)
        }

        fetchDocumentTypes()
    }, [])

    const timeout = (ms: number) => {
        return new Promise(resolve => setTimeout(resolve, ms));
    }


    const uploadFiles = async () => {
        if (files && files.length > 0) {
            setIsLoading(true)
            setError(null)
            const response = await API.post(apiName, '/api/training/create', {})

            const jobId = response.id
            console.log(`Using Job Id - ${jobId}`)
            for (const file of files) {
                const urlResponse = await API.get(apiName, '/api/training/url', {
                    queryStringParameters: {
                        JobId: jobId,
                        S3Key: file.name
                    }
                })

                try {
                    await Utils.uploadFile(urlResponse, file.data, "image/jpeg")
                    setFileCount(fileCount + 1)
                    setStatus(`Uploaded ${fileCount} files(s)`)
                } catch (err: any) {
                    if (err.response && err.response.data) {
                        setError(err.response.data.error);
                        setIsLoading(false)
                        console.log(err.response.data.error);
                    } else setError("An error occurred uploading the files.");
                    break
                }

                //wait(1000) // Need to slow things down so that S3 doesn't throttle us
                await timeout(1000)
            }

            try {
                await API.post(apiName, '/api/training/start', {
                    queryStringParameters: {
                        JobId: jobId
                    }
                })

                setMsg(`Successfully created training job ${jobId}`)
                setIsLoading(false)
                //history.push("/newjob")

            } catch (err: any) {

                if (err.response && err.response.data) {
                    setError(err.response.data.error);
                    setIsLoading(false)
                    console.log(err.response.data.error);
                } else setError("An error occurred uploading starting the Ground Truth job.");


            }

        } else
            setStatus('No files to upload.')


    }

    return (
        <div>
            <Container style={{display: isLoading ? 'none' : 'block'}} headingVariant='h4'
                       title="Create a new Training Job"
                       subtitle="This allows you to create and start a new Rekognition custom labels bounding box training job.">
                <Stack spacing='s'>

                    <div style={{display: error ? "block" : "none"}}>
                        <Alert type="error">{error}</Alert>
                    </div>
                    <div style={{display: msg ? "block" : "none"}}>
                        <Alert type="success">{msg}</Alert>
                    </div>
                    <Text>Training jobs help to train the Rekognition Custom Labels model that can help with removal of
                        background clutter in a document image. To train a model, you should have a collection of each
                        type of document, with a variety of backgrounds.</Text>
                    <div style={{display: isLoading ? 'none' : 'block'}}>
                        <Stack spacing='xs'>
                            <Text ><b>Here are the document types currently supported: {docTypes}</b></Text>


                            <Text>Start by uploading a series of images. For each document type, you should aim to
                                include at least 30 images, with a variety of backgrounds.</Text>
                        </Stack>

                    </div>

                    <input
                        type="file"
                        accept="image/jpeg,image/png"
                        onChange={onFileUploadChanged}
                        multiple
                    />
                    <Button variant="primary" onClick={uploadFiles}>
                        Upload Files
                    </Button>
                </Stack>
            </Container>
            <div style={{display: status ? "block" : "none"}}>
                <Text>{status}</Text>
            </div>
            <div style={{display: isLoading ? 'block' : 'none'}}>
                <LoadingIndicator label='Uploading'/>
            </div>
        </div>
    )

}


export default NewTrainingJob
