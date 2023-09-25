import {useMutation, useQuery} from "react-query";
import {API} from "aws-amplify";
import {apiName} from "../../constants";
import axios from "axios";

export interface VerificationSession {
    id: string
}

export interface FaceLivenessSessionResult {

    livenessCheckSessionId?: string
    verified: boolean
    confidence?: number
}

export interface VerificationHistoryItem {
    sessionId: string
    error?: string
    time: Date
    documentType: string
    isSuccessful: boolean
    client: string
}

export interface CreateLivenessSessionResult {
    sessionId: string,
    livenessSessionId: string
}

export interface CompareSelfieResult {
    isSimilar?: boolean
    similarity: number
}

export interface GetImageUrlsResult {
    selfieUrl: string
    documentUrl: string
    sessionId: string
}

export const useGetVerificationHistory = () => {
    return useQuery<VerificationHistoryItem[]>("getVerificationHistory", async () => {
        const response = await API.get(apiName, `/api/history`, {})

        console.log(`Get verification history response: ${JSON.stringify(response)}`)

        return response as VerificationHistoryItem[]
    })
}
export const useCompareSelfieWithDocument = () => {
    return useMutation("compare", async (data: { sessionId: string }) => {
        const response = await API.post(apiName, "/api/session/compare", {
            queryStringParameters: {
                'sessionId': data.sessionId,
            }
        })
        console.log(`useCompareSelfieWithDocument response: ${JSON.stringify(response)}`)
        return response as CompareSelfieResult
    })
}

export const useGetImageUrls = (sessionId: string) => {
    return useQuery<GetImageUrlsResult>("getVerificationHistory", async () => {
        const response = await API.get(apiName, `/api/session/image/url/${sessionId}`, {})

        console.log(`Get image URLs response: ${JSON.stringify(response)}`)

        return response as GetImageUrlsResult
    }, {enabled: !!sessionId})
}


export const useSubmitDocumentForVerification = () => {
    return useMutation("submitdocument", async (data: { sessionId: string, s3Key: string, expectedDocumentType: string }) => {
        await API.post(apiName, "/api/session/document", {
            queryStringParameters: {
                'sessionId': data.sessionId,
                's3Key': data.s3Key,
                'expectedDocumentType': data.expectedDocumentType
            }
        })

    })
}
export const useUploadFile = () => {
    return useMutation("uploadFile", async (data: { sessionId: string, s3Key: string, uploadedFile: any }) => {
        const url = await getPresignedUrl(data.sessionId, data.s3Key)

        await axios.put(url, data.uploadedFile)


    })
}

const getPresignedUrl = async (sessionId: string, s3Key: string) => {
    const response = await API.get(apiName, `/api/session/url`, {
        queryStringParameters: {
            'sessionId': sessionId,
            's3Key': s3Key
        }
    })

    return response
}


export const useStartVerificationSession = () => {

    return useMutation<VerificationSession>("startsession", async () => {
        const response = await API.post(apiName, "/api/session/new", {})
        return response as VerificationSession
    })
}


export const useCreateLivenessSession = () => {
    return useMutation("createliveness", async (data: { sessionId?: string, sessionToken?: string }) => {

        if (!data.sessionId || !data.sessionToken) {
            console.log(`Session ID or token is undefined. ${data.sessionId} ${data.sessionToken}`)
            return
        }

        const response = await API.post(apiName, `/api/liveness/createsession/${data.sessionId}/${data.sessionToken}`, {})

        console.log(`Liveness session ID: ${response.sessionId}`)

        return response as CreateLivenessSessionResult
    })
}

export const useGetLivenessResult = (verificationDone: boolean, sessionId?: string, livenessSessionId?: string) => {
    return useQuery<FaceLivenessSessionResult>(`getliveness`, async () => {
        const response = await API.get(apiName, `/api/liveness/getsessionresult/${sessionId}/${livenessSessionId}`, {})

        console.log(`Liveness session response: ${JSON.stringify(response)}`)

        return response as FaceLivenessSessionResult
    }, {enabled: !!sessionId && !!livenessSessionId && verificationDone})

}