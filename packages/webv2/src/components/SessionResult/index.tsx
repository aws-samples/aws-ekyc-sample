import React, {FC, useEffect} from "react";
import {Alert, Container, Header, SpaceBetween, Spinner, TextContent} from "@cloudscape-design/components";
import {useCompareSelfieWithDocument, useGetImageUrls} from "../../hooks/Verification";

export const SessionResult: FC<{ sessionId: string }> = ({sessionId}) => {
    const {
        data: compareSelfieWithDocumentData,
        mutate: compareSelfieWithDocument,
        isLoading: isLoadingCompareSelfie,
        isSuccess: isSuccessCompareSelfie,
        isError: isErrorCompareSelfie
    } = useCompareSelfieWithDocument()

    const {data: imageUrlData, isLoading: isLoadingImageUrlData} = useGetImageUrls(sessionId)

    useEffect(() => {
        compareSelfieWithDocument({sessionId: sessionId})
    }, [])

    return (
        <SpaceBetween size={"m"}>
            {isErrorCompareSelfie &&
                <Alert type={"error"}>An error occurred comparing selfies.</Alert>
            }
            {isLoadingCompareSelfie &&
                <div><Spinner></Spinner>Loading....</div>
            }
            {!isLoadingImageUrlData && compareSelfieWithDocumentData &&
                <SpaceBetween size={"m"}>
                    <TextContent>{compareSelfieWithDocumentData.isSimilar ? "Faces match" : "Faces do not match."}</TextContent>
                    
                </SpaceBetween>
            }
            {!isLoadingImageUrlData && imageUrlData && compareSelfieWithDocumentData &&
                <Container>
                    <SpaceBetween size={"m"}><Header>Liveness Image</Header>
                        <img width={720} src={imageUrlData?.selfieUrl}/>
                    </SpaceBetween>
                    <img width={50} height={50}
                         src={compareSelfieWithDocumentData.isSimilar ? "tick.jpeg" : "cross.png"}/>
                    <SpaceBetween size={"m"}><Header>Document Image</Header>
                        <img width={720} src={imageUrlData?.documentUrl}/>
                    </SpaceBetween>
                </Container>
            }
        </SpaceBetween>
    )
}