import {useMutation, useQuery} from "@tanstack/react-query";
import {apiGet, apiPost} from "../../apiClient";
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
    return useMutation<TrainingJob, Error>({
        mutationKey: ["createTrainingJob"],
        mutationFn: () => apiPost<TrainingJob>("/api/training/create")
    })
}
const timeout = (ms: number) => {
    return new Promise(resolve => setTimeout(resolve, ms));
}
export const useCreateJobAndUpload = () => {
    return useMutation({
        mutationKey: ["createJobAndUpload"],
        mutationFn: async (uploadData: { files: FileToUpload[] }) => {
            const job = await apiPost<TrainingJob>("/api/training/create")

            for (const file of uploadData.files) {
                const urlResponse = await apiGet<string>('/api/training/url', {
                    JobId: job.id,
                    S3Key: file.name
                })

                await uploadFile(urlResponse, file.data, "image/jpeg")

                // Need to slow things down so that S3 doesn't throttle us
                await timeout(1000)
            }
            const arn = await apiPost<string>('/api/training/start', {
                queryParams: {JobId: job.id}
            })

            return arn
        }
    })
}

export const useGetTrainingJobs = () => {
    return useQuery<TrainingJob[]>({
        queryKey: ["getTrainingJobs"],
        queryFn: async () => {
            return (await apiGet<TrainingJob[]>("/api/training/list"))
                .sort((a, b) => b.startTime - a.startTime)
        }
    })
}
