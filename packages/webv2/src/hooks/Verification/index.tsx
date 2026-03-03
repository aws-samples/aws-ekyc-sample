import {useMutation, useQuery} from "@tanstack/react-query";
import {apiGet, apiPost} from "../../apiClient";
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
    return useQuery<VerificationHistoryItem[]>({
        queryKey: ["getVerificationHistory"],
        queryFn: async () => {
            const response = await apiGet<VerificationHistoryItem[]>(`/api/history`)
            console.log(`Get verification history response: ${JSON.stringify(response)}`)
            return response
        }
    })
}
export const useCompareSelfieWithDocument = () => {
    return useMutation({
        mutationKey: ["compare"],
        mutationFn: async (data: { sessionId: string }) => {
            const response = await apiPost<CompareSelfieResult>("/api/session/compare", {
                queryParams: {'sessionId': data.sessionId}
            })
            console.log(`useCompareSelfieWithDocument response: ${JSON.stringify(response)}`)
            return response
        }
    })
}

export const useGetImageUrls = (sessionId: string) => {
    return useQuery<GetImageUrlsResult>({
        queryKey: ["getImageUrls", sessionId],
        queryFn: () => apiGet<GetImageUrlsResult>(`/api/session/image/url/${sessionId}`),
        enabled: !!sessionId
    })
}


export const useSubmitDocumentForVerification = () => {
    return useMutation({
        mutationKey: ["submitdocument"],
        mutationFn: async (data: { sessionId: string, s3Key: string, expectedDocumentType: string }) => {
            await apiPost("/api/session/document", {
                queryParams: {
                    'sessionId': data.sessionId,
                    's3Key': data.s3Key,
                    'expectedDocumentType': data.expectedDocumentType
                }
            })
        }
    })
}
export const useUploadFile = () => {
    return useMutation({
        mutationKey: ["uploadFile"],
        mutationFn: async (data: { sessionId: string, s3Key: string, uploadedFile: any }) => {
            const url = await getPresignedUrl(data.sessionId, data.s3Key)
            await axios.put(url, data.uploadedFile)
        }
    })
}

const getPresignedUrl = async (sessionId: string, s3Key: string) => {
    return apiGet<string>(`/api/session/url`, {
        'sessionId': sessionId,
        's3Key': s3Key
    })
}


export const useStartVerificationSession = () => {
    return useMutation<VerificationSession, Error>({
        mutationKey: ["startsession"],
        mutationFn: () => apiPost<VerificationSession>("/api/session/new")
    })
}


export const useCreateLivenessSession = () => {
    return useMutation({
        mutationKey: ["createliveness"],
        mutationFn: async (data: { sessionId?: string, sessionToken?: string }) => {

            if (!data.sessionId || !data.sessionToken) {
                console.log(`Session ID or token is undefined. ${data.sessionId} ${data.sessionToken}`)
                return
            }

            const response = await apiPost<CreateLivenessSessionResult>(`/api/liveness/createsession/${data.sessionId}/${data.sessionToken}`)
            console.log(`Liveness session ID: ${response.sessionId}`)
            return response
        }
    })
}

export const useGetLivenessResult = (verificationDone: boolean, sessionId?: string, livenessSessionId?: string) => {
    return useQuery<FaceLivenessSessionResult>({
        queryKey: ["getliveness", sessionId, livenessSessionId],
        queryFn: async () => {
            const response = await apiGet<FaceLivenessSessionResult>(`/api/liveness/getsessionresult/${sessionId}/${livenessSessionId}`)
            console.log(`Liveness session response: ${JSON.stringify(response)}`)
            return response
        },
        enabled: !!sessionId && !!livenessSessionId && verificationDone
    })

}
