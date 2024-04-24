import React, {FC, useEffect, useState} from "react";
import {Alert, Box, Container, Header, Link, SpaceBetween, Spinner, Wizard} from "@cloudscape-design/components";
import {
    FaceLivenessSessionResult,
    useStartVerificationSession,
    useSubmitDocumentForVerification
} from "../../hooks/Verification";
import {useNavigate} from "react-router-dom";
import {Liveness} from "../Liveness";
import {DocumentUploader} from "../DocumentUploader";
import {SessionResult} from "../SessionResult";

export const VerificationWizard: FC = () => {
    const navigate = useNavigate()

    const [
        activeStepIndex,
        setActiveStepIndex
    ] = useState(0);

    const [validationError, setValidationError] = useState("")
    const [documentType, setDocumentType] = useState("")
    const [faceLivenessResult, setFaceLivenessResult] = useState<FaceLivenessSessionResult | undefined>(undefined)

    const [verificationSessionId, setVerificationSessionId] = useState("")
    const {
        mutate: submitDocumentForVerification,
        isLoading: isLoadingSubmitDocumentForVerification
    } = useSubmitDocumentForVerification()

    const {
        mutate: startVerificationSession, isLoading: isLoadingStartVerificationSession, isSuccess:
            isSuccessStartVerificationSession, data: newVerificationSession
    } = useStartVerificationSession()

    const [uploadedFile, setUploadedFile] = React.useState<any>(null);

    const onFileUploaded = (s3Key: string, documentType: string, uploadedFile: any) => {
        console.log(`Received onFileUploaded event`)
        setDocumentType(documentType)
        setUploadedFile(uploadedFile)
        submitDocumentForVerification({
            s3Key: s3Key,
            sessionId: verificationSessionId,
            expectedDocumentType: documentType
        })
    }

    const navigateToVerify = () => {
        navigate("/verify")
    }

    useEffect(() => {

        if (!verificationSessionId)
            startVerificationSession()
    }, [])


    const onVerificationResult = (result: FaceLivenessSessionResult) => {
        setFaceLivenessResult(result)
    }

    useEffect(() => {
        if (!isLoadingStartVerificationSession && isSuccessStartVerificationSession && newVerificationSession?.id) {
            console.log(`Setting new verification session ${newVerificationSession?.id}`)

            setVerificationSessionId(newVerificationSession?.id)
        }
    }, [isLoadingStartVerificationSession, isSuccessStartVerificationSession, newVerificationSession])

    if (isLoadingStartVerificationSession || !verificationSessionId)
        return <Box padding={"m"}><Alert type={"info"}>Creating new verification session.</Alert></Box>


    return (
        <Box padding={"m"}>
            {isLoadingSubmitDocumentForVerification &&
                <><Spinner></Spinner>Uploading document....</>
            }
            {validationError &&
                <Alert key={"uploadvalidationerror"}
                       type={"warning"}>{validationError}</Alert>
            }
            <Wizard
                i18nStrings={{
                    stepNumberLabel: stepNumber =>
                        `Step ${stepNumber}`,
                    collapsedStepsLabel: (stepNumber, stepsCount) =>
                        `Step ${stepNumber} of ${stepsCount}`,
                    skipToButtonLabel: (step, stepNumber) =>
                        `Skip to ${step.title}`,
                    navigationAriaLabel: "Steps",
                    cancelButton: "Cancel",
                    previousButton: "Previous",
                    nextButton: "Next",
                    submitButton: "Complete Verification",
                    optional: "optional"
                }}
                onNavigate={({detail}) => {
                    if (detail.requestedStepIndex === 1) {
                        // Validate the form
                        if (!uploadedFile) {
                            setValidationError("Please upload a file.")
                            return
                        }
                        if (!documentType) {
                            setValidationError("Please select a document type.")
                            return
                        }

                        setValidationError("")
                        //  submitDocumentForVerification({sessionId,s3Key:})
                    } else if (detail.requestedStepIndex === 2) {
                        if (!faceLivenessResult?.verified) {
                            setValidationError("Please perform liveness verification.")
                            return
                        }

                    }
                    setActiveStepIndex(detail.requestedStepIndex)
                }
                }
                onSubmit={navigateToVerify}
                onCancel={navigateToVerify}
                activeStepIndex={activeStepIndex}
                allowSkipTo
                steps={[
                    {
                        title: "Start Document Verification",
                        info: <Link variant="info">Info</Link>,
                        description:
                            "This wizard will help you verify your document.",
                        content: (
                            <Container
                                header={
                                    <Header variant="h2">
                                        Upload Document
                                    </Header>
                                }
                            >
                                <SpaceBetween size={"m"}>

                                    <div key={"session_id_text"}>Verification Session ID: {verificationSessionId}</div>
                                    <DocumentUploader initialSelectedDocumentType={documentType}
                                                      sessionId={verificationSessionId}
                                                      fileUploaded={onFileUploaded}></DocumentUploader>

                                </SpaceBetween>

                            </Container>
                        )
                    },
                    {
                        title: "Verify Liveness",
                        info: <Link variant="info">Info</Link>,
                        description:
                            "Please follow the instructions.",
                        content: (
                            <Container
                                header={
                                    <Header variant="h2">
                                        Verify Liveness
                                    </Header>
                                }
                            >
                                <Liveness sessionId={verificationSessionId} onVerificationResult={onVerificationResult}
                                ></Liveness>
                            </Container>
                        )
                    },

                    {
                        title: "Check Results",
                        info: <Link variant="info">Info</Link>,

                        content: (
                            <Container
                                header={
                                    <Header variant="h2">
                                        Check Results
                                    </Header>
                                }
                            >
                                <SessionResult sessionId={verificationSessionId}></SessionResult>
                            </Container>
                        )
                    },
                ]}
            />
        </Box>
    )
}