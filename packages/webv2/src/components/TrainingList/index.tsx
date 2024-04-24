import {FC} from "react";
import {Alert, Box, Button, Header, SpaceBetween, Spinner, Table, TextFilter} from "@cloudscape-design/components";
import {useGetTrainingJobs} from "../../hooks/Training";
import {useNavigate} from "react-router-dom";

export const TrainingList: FC = () => {
    const navigate = useNavigate()
    const {isLoading, data: trainingJobs, isError} = useGetTrainingJobs()


    const newJob = () => {
        navigate("/newtraining")
    }


    return (
        <Box padding={"m"}><SpaceBetween size={"m"}>
            {isError &&
                <Alert type={"error"}>An error occurred retrieving training jobs.</Alert>
            }
            {isLoading &&
                <Spinner></Spinner>
            }
            {!isLoading && !isError &&
                <>
                    <Box textAlign={"right"}>
                        <Button variant={"primary"} onClick={newJob}>New Training Job</Button>
                    </Box>
                    <Table
                        columnDefinitions={[
                            {
                                id: "id",
                                header: "ID",
                                cell: item => item.id,
                                sortingField: "id",

                            },
                            {
                                id: "time",
                                header: "Date/Time",
                                cell: item => new Date(item.startTime).toLocaleDateString() + ' ' + new Date(item.startTime).toLocaleTimeString(),
                                sortingField: "time"
                            },
                            {
                                id: "jobArn",
                                header: "Labelling Job ARN",
                                cell: item => item.labellingJobArn
                            },
                            {
                                id: "Status",
                                header: "Status",
                                cell: item => item.status
                            }
                        ]}
                        items={trainingJobs ?? []}
                        loadingText="Loading history"
                        trackBy="name"
                        empty={
                            <Box
                                margin={{vertical: "xs"}}
                                textAlign="center"
                                color="inherit"
                            >
                                <SpaceBetween size="m">
                                    <b>No Training Jobs</b>
                                    <Button onClick={newJob}>New Training Job</Button>
                                </SpaceBetween>
                            </Box>
                        }
                        filter={
                            <TextFilter
                                filteringPlaceholder="Find training job"
                                filteringText=""
                            />
                        }
                        header={
                            <Header
                            >
                                Training Jobs

                            </Header>
                        }


                    /></>
            }
        </SpaceBetween></Box>)
}