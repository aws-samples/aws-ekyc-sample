import React, {FC, useEffect, useMemo, useState} from "react";
import {Alert, Button, Header, Icon, Select, SpaceBetween, Spinner} from "@cloudscape-design/components";
import {DocumentType, useGetDocumentTypes} from "../../hooks/FieldData";
import {SelectProps} from "@cloudscape-design/components/select/interfaces";
import {useUploadFile} from "../../hooks/Verification";

export const DocumentUploader: FC<{
    initialSelectedDocumentType?: string,
    sessionId: string,
    fileUploaded: (s3Key: string, documentType: string, uploadedFile: any) => void
}> = ({
          fileUploaded,
          sessionId,
          initialSelectedDocumentType
      }) => {


    const {
        isLoading: isLoadingDocumentTypes,
        isSuccess: isSuccessDocumentTypes,
        isError: isErrorDocumentTypes,
        data: dataDocumentTypes
    } = useGetDocumentTypes()


    const [uploadedFile, setUploadedFile] = React.useState<any>(null);
    const [imgData, setImgData] = useState<any>(null);
    const [documentTypes, setDocumentTypes] = React.useState<DocumentType[]>([]);

    const [isLoading, setIsLoading] = useState(false)
    const [errorText, setErrorText] = useState("")

    const [selectedDocumentType, setSelectedDocumentType] = React.useState<SelectProps.Option | null>(null);

    const [documentTypeOptions, setDocumentTypeOptions] = React.useState<SelectProps.Option[]>([]);

    const s3Key = "document.jpg";

    const {mutate: uploadFile, isLoading: isUploadFileLoading, isSuccess: isUploadFileSuccess} = useUploadFile()

    useEffect(() => {
        if (isUploadFileSuccess && selectedDocumentType) {
            console.log(`File uploaded`)
            fileUploaded(s3Key, selectedDocumentType.value!, selectedDocumentType)
        }
    }, [isUploadFileSuccess])


    useEffect(() => {

        if (dataDocumentTypes)
            setDocumentTypes(dataDocumentTypes)
        else
            setDocumentTypes([])

        const options = dataDocumentTypes?.map(a => {
            return {label: a.name, value: a.code}
        }) ?? []

        setDocumentTypeOptions(options)

        if (initialSelectedDocumentType) {
            console.log(`intial document type: ${initialSelectedDocumentType}`)
            setSelectedDocumentType(
                documentTypeOptions.find((o) => o.value === initialSelectedDocumentType) ?? null
            );
        }
    }, [dataDocumentTypes, isLoadingDocumentTypes, isSuccessDocumentTypes])

    const onChange = (newValue?: string) =>
        setSelectedDocumentType(
            documentTypeOptions.find((o) => o.value === newValue) ?? null
        );

    const onFileUploadChanged = (event: any) => {
        if (event.target.files[0]) {
            setUploadedFile(event.target.files[0]);

            const reader = new FileReader();
            reader.addEventListener("load", () => {
                setImgData(reader.result);
            });
            reader.readAsDataURL(event.target.files[0]);
        }
    };


    const useUploadedFile = (element: any) => {
        if (!selectedDocumentType) {
            setErrorText("Document type not selected.");
            return;
        }

        if (uploadedFile === null) {
            setErrorText("No file selected.");
            return;
        }

        console.log(`Using file ${uploadedFile}`);

        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore
        const documentType = documentTypes.find(
            (o) => o.code === selectedDocumentType.value
        );

        if (!documentType) {
            setErrorText("Document type not found.");
            return;
        }

        console.log(`Document type - ${documentType?.code}`);
        uploadFile({sessionId, s3Key, uploadedFile})
    }

    const error = useMemo(() => {
        let errorString = ""
        if (isErrorDocumentTypes) {
            errorString += "Error loading document types."
        }
        return errorString
    }, [isErrorDocumentTypes])

    const isLoadingAnything = isLoadingDocumentTypes || isLoading

    const isSuccess = isSuccessDocumentTypes

    return (
        <div>
            {errorText &&
                <div>
                    <Alert type="error">{errorText}</Alert>
                </div>
            }
            <Header variant={"h3"}>Choose the document type</Header>
            {!isLoadingAnything &&
                <>
                    <SpaceBetween size="m">
                        <Select key={"documentTypeSelect"}
                                placeholder="Choose a document type"
                                options={documentTypeOptions}
                                selectedOption={selectedDocumentType}
                                onChange={(e) => onChange(e.detail.selectedOption?.value)}
                                loadingText="Loading"
                        />

                        <input key={"uploadfile"}
                               type="file"
                               accept="image/jpeg,image/png"
                               onChange={onFileUploadChanged}
                        />
                        {imgData &&
                            <div>
                                <img width={720} src={imgData ?? ""} alt={""}/>
                            </div>
                        }
                        {!isUploadFileLoading &&
                            < Button variant="primary" onClick={useUploadedFile}>
                                Upload File
                            </Button>
                        }
                        {isUploadFileLoading &&
                            <Spinner></Spinner>
                        }
                        {
                            isUploadFileSuccess &&
                            <SpaceBetween size={"m"} direction={"horizontal"}>
                                <Icon name={"check"}></Icon>
                                <div> File uploaded.</div>
                            </SpaceBetween>

                        }
                    </SpaceBetween>
                </>
            }
        </div>)
}