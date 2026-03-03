import {useMutation, useQuery} from "@tanstack/react-query";
import {apiGet} from "../../apiClient";

export interface EkycSettings {
    RekognitionCustomLabelsProjectArn?: string
}

export const useGetSettings = () => {
    return useQuery<EkycSettings>({
        queryKey: ["getSettings"],
        queryFn: () => apiGet<EkycSettings>("/api/settings")
    })
}

export const useSaveSettings = () => {
    return useMutation({
        mutationKey: ["saveSettings"],
        mutationFn: async (settings: EkycSettings) => {

        }
    })
}