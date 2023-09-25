import {useMutation, useQuery} from "react-query";
import {API} from "aws-amplify";
import {apiName} from "../../constants";
import axios from "axios";
import {Buffer} from 'buffer';

export interface TrainingJob {
    id: string
    startTime: number
    status: string
    labellingJobArn?: string
}

export interface FileToUpload {
    data: any
    name: string
}

export const stripBase64Chars = async (data: string) => {

    return data.replace(/^data:image\/[a-z]+;base64,/, "")

}
export const uploadFile = async (url: string, toUpload: string, contentType: string) => {
    console.log(toUpload)
    const strippedData = await stripBase64Chars(toUpload)
    const buff = Buffer.from(strippedData, 'base64')
    await axios.put(url, buff, {headers: {'Content-Type': contentType}})
}

export const useCreateTrainingJob = () => {
    return useMutation("createTrainingJob", async () => {
        const response = await API.post(apiName, "/api/training/create", {})
        return response as TrainingJob
    })
}
const timeout = (ms: number) => {
    return new Promise(resolve => setTimeout(resolve, ms));
}
export const useCreateJobAndUpload = () => {
    return useMutation("createTrainingJob", async (uploadData: { files: FileToUpload[] }) => {
        const response = await API.post(apiName, "/api/training/create", {})
        const job = response as TrainingJob

        for (const file of uploadData.files) {
            const urlResponse = await API.get(apiName, '/api/training/url', {
                queryStringParameters: {
                    JobId: job.id,
                    S3Key: file.name
                }
            })

            await uploadFile(urlResponse, file.data, "image/jpeg")

            // Need to slow things down so that S3 doesn't throttle us
            await timeout(1000)
        }
        const arn = (await API.post(apiName, '/api/training/start', {
            queryStringParameters: {
                JobId: job.id
            }
        })) as string

        return arn
    })
}

export const useGetTrainingJobs = () => {
    return useQuery<TrainingJob[]>("getTrainingJobs", async () => {
        return (await API.get(apiName, "/api/training/list", {}))
            .map((a: any) => a as TrainingJob)
            .sort((a: TrainingJob, b: TrainingJob) => b.startTime - a.startTime)
    })
}