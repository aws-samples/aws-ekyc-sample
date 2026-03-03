import {useQuery} from "@tanstack/react-query";
import {apiGet} from "../../apiClient";

export interface DocumentType {
    name: string
    code: string
}

export const useGetDocumentTypes = () => {
    return useQuery<DocumentType[]>({
        queryKey: ["documenttypes"],
        queryFn: () => apiGet<DocumentType[]>("/api/document/doctypes")
    })
}

