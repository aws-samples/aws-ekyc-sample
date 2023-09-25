import {FC} from "react";
import {useGetVerificationHistory} from "../../hooks/Verification";
import {Alert, Box, Button, Header, SpaceBetween, Table, TextFilter} from "@cloudscape-design/components";
import {useNavigate} from "react-router-dom";

export const VerificationList: FC = () => {

    const {data: verificationHistory, isLoading, isError} = useGetVerificationHistory()
    const navigate = useNavigate()
    if (isLoading)
        return <Alert type={"info"}>Loading...</Alert>

    if (isError)
        return <Alert type={"error"}>An error occurred loading verification history.</Alert>

    const newVerification = () => {
        navigate("/newverification")
    }

    return (<Box padding={"m"}><SpaceBetween size={"m"}>
            <Box textAlign={"right"}>
                <Button variant={"primary"} onClick={newVerification}>New Verification</Button>
            </Box>
            <Table
                columnDefinitions={[
                    {
                        id: "id",
                        header: "ID",
                        cell: item => item.sessionId,
                        sortingField: "sessionId",

                    },
                    {
                        id: "time",
                        header: "Date/Time",
                        cell: item => item.time.toLocaleDateString() + ' ' + item.time.toLocaleTimeString(),
                        sortingField: "time"
                    },
                    {
                        id: "documentType",
                        header: "Document Type",
                        cell: item => item.documentType
                    },
                    {
                        id: "Status",
                        header: "Status",
                        cell: item => item.isSuccessful ? "Success" : "Fail"
                    }
                ]}

                items={verificationHistory ?? []}
                loadingText="Loading history"
                selectionType="multi"
                trackBy="name"
                empty={
                    <Box
                        margin={{vertical: "xs"}}
                        textAlign="center"
                        color="inherit"
                    >
                        <SpaceBetween size="m">
                            <b>No History</b>
                            <Button onClick={newVerification}>New Verification</Button>
                        </SpaceBetween>
                    </Box>
                }
                filter={
                    <TextFilter
                        filteringPlaceholder="Find verification"
                        filteringText=""
                    />
                }
                header={
                    <Header

                    >
                        Verification History

                    </Header>
                }


            /></SpaceBetween></Box>
    )
}