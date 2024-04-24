import {useQuery} from "react-query";
import {API} from "aws-amplify";
import {apiName} from "../../constants";

export interface DocumentType {
    name: string
    code: string

}

export const useGetDocumentTypes = () => {
    return useQuery<{ name: string, code: string }[]>("documenttypes", async () => {
        return (await API.get(apiName, "/api/document/doctypes", {}))
            .map((a: any) => a as DocumentType)

    })
}

