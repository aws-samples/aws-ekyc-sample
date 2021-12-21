import React, {useEffect, useState} from 'react';
import Table, {Column} from 'aws-northstar/components/Table';
import {API} from "aws-amplify";
import Utils from "../../Utils";
import {Container, Stack} from "aws-northstar";
import Button from "aws-northstar/components/Button";
import LoadingIndicator from "aws-northstar/components/LoadingIndicator";
import {useHistory} from "react-router-dom";

const apiName = 'ekycApi'


const TrainingJobsTable = () => {

    const [isLoading, setIsLoading] = useState(true)


    const [data, setData] = useState([])

    const history = useHistory();

    useEffect(() => {
        const fetchJobs = async () => {

            const response = await API.get(apiName, '/api/training/list', {})

            setData(response)
            setIsLoading(false)
        }


        fetchJobs()


    }, [])

    interface DataType {
        id: string;
        startTime: number;
        status: string
        projectVersionArn: string
        labellingJobArn: string
    }


    const columnDefinitions: Column<DataType>[] = [
        {
            id: 'id',
            width: 100,
            Header: 'Id',
            accessor: 'id',
        },
        {
            id: 'status',
            width: 200,
            Header: 'Status',
            accessor: 'status'
        },
        {
            id: 'startTime',
            width: 200,
            Header: 'Started At',
            accessor: row => Utils.getDateTimeString(row.startTime * 1000)
        },
        {
            id: 'labellingJobArn',
            width: 400,
            Header: 'Labelling Job',
            accessor: 'labellingJobArn'
        },
        {
            id: 'projectVersionArn',
            width: 400,
            Header: 'Project Version',
            accessor: 'projectVersionArn'
        },


    ]


    return (

        <div>
            <Container
                actionGroup={<Button onClick={() => {
                    history.push("/newjob");
                }} variant='primary'>New Training Job</Button>}
            >
                <Stack spacing='s'>

                    <div style={{display: isLoading ? 'none' : 'block'}}>
                        <Table
                            tableTitle='Training Jobs'
                            loading={false}
                            items={data}
                            columnDefinitions={columnDefinitions}
                            disableGroupBy={true}
                            disableSettings={true}
                            disablePagination={true}
                            disableFilters={true}
                            disableRowSelect={true}
                            disableSortBy={true}
                        />
                    </div>
                    <div style={{display: isLoading ? 'block' : 'none'}}>
                        <LoadingIndicator label='Loading'/>
                    </div>
                </Stack>
            </Container>
        </div>)
}

export default TrainingJobsTable
