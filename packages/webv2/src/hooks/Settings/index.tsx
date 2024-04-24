import {useMutation, useQuery} from "react-query";
import {API} from "aws-amplify";
import {apiName} from "../../constants";

export interface EkycSettings {
    RekognitionCustomLabelsProjectArn?: string
}

export const useGetSettings = () => {
    return useQuery<EkycSettings>("getSettings", async () => {
        return (await API.get(apiName, "/api/settings", {})) as EkycSettings

    })
}

export const useSaveSettings = () => {
    return useMutation("saveSettings", async (settings: EkycSettings) => {

    })
}