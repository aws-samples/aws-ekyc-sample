import {FC, useEffect, useState} from "react";
import {Alert, Header, Icon, SpaceBetween} from "@cloudscape-design/components";
import {AwsCredentialProvider, FaceLivenessDetectorCore} from '@aws-amplify/ui-react-liveness';
import {Auth} from "aws-amplify";
import {v4 as uuidv4} from "uuid"
import {FaceLivenessSessionResult, useCreateLivenessSession, useGetLivenessResult} from "../../hooks/Verification";

export const Liveness: FC<{
    sessionId?: string
    onVerificationResult: (result: FaceLivenessSessionResult) => void
}> = ({onVerificationResult, sessionId}) => {

    const [livenessSessionId, setLivenessSessionId] = useState("")
    const [verificationDone, setVerificationDone] = useState(false)
    const [createLivenessApiData, setCreateLivenessApiData] = useState<{
        sessionId: string;
    } | null>(null);

    const [sessionToken, setSessionToken] = useState<string | undefined>(undefined)

    const {
        data: checkLivenessResponseResult,
        refetch: refetchLivenessResponse,
        isLoading: isCheckLivenessLoading, isError: isCheckLivenessError
    } = useGetLivenessResult(verificationDone, sessionId, livenessSessionId)

    const {
        data: createLivenessSessionResponse,
        mutate: createLivenessSession,
        isLoading: isLoadingCreateLivenessSession,
        isError: isCreateLivenessSessionError
    } = useCreateLivenessSession()


    const credentialProvider: AwsCredentialProvider = async () => {
        // Fetch the credentials
        const creds = await Auth.currentCredentials()

        return {
            accessKeyId: creds.accessKeyId,
            secretAccessKey: creds.secretAccessKey,
            sessionToken: creds.sessionToken
        }
    }

    useEffect(() => {
        if (createLivenessSessionResponse?.livenessSessionId)
            setLivenessSessionId(createLivenessSessionResponse?.livenessSessionId)
    }, [isLoadingCreateLivenessSession, createLivenessSessionResponse])

    useEffect(() => {

        if (checkLivenessResponseResult && !isCheckLivenessLoading) {

            onVerificationResult(checkLivenessResponseResult)
        }

    }, [verificationDone, checkLivenessResponseResult, isCheckLivenessLoading])

    useEffect(() => {
        const token = uuidv4()
        setSessionToken(token)

    }, [])

    useEffect(() => {
        console.log('Creating new liveness session')
        createLivenessSession({sessionId: sessionId, sessionToken: sessionToken})
    }, [sessionToken])


    const onCancel = () => {

        setSessionToken(uuidv4())
    }

    const handleAnalysisComplete: () => Promise<void> = async () => {
        /*
         * This should be replaced with a real call to your own backend API
         */
        setVerificationDone(true)

        await refetchLivenessResponse()

    };

    if (isLoadingCreateLivenessSession || isCheckLivenessLoading || !createLivenessSessionResponse)
        return <Alert type={"info"}>Loading...</Alert>

    if (isCheckLivenessError || isCreateLivenessSessionError)
        return <Alert type={"error"}>An error occurred.</Alert>

    return (
        <>
            {!verificationDone &&
                < FaceLivenessDetectorCore
                    sessionId={createLivenessSessionResponse?.livenessSessionId!}
                    region="ap-northeast-1"
                    config={{credentialProvider}}
                    onUserCancel={onCancel}
                    onAnalysisComplete={handleAnalysisComplete}
                    onError={(error: any) => {
                        console.error(error);
                        setVerificationDone(false)
                        setSessionToken(uuidv4())
                    }}
                />
            }

            {verificationDone && checkLivenessResponseResult &&
                <SpaceBetween size={"s"}>
                    {checkLivenessResponseResult.verified &&
                        <Header
                            variant={"h2"}><Icon name="check"/> Identity is live</Header>

                    }
                    {!checkLivenessResponseResult.verified &&
                        <Header
                            variant={"h2"}><Icon name="status-warning"/> Identity is not live</Header>

                    }
                    <div>Confidence: {checkLivenessResponseResult.confidence}</div>
                </SpaceBetween>

            }
        </>
    );

}