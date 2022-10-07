/* eslint-disable react/prop-types */
import React, {useEffect, useState} from 'react';
import Table, {Column} from 'aws-northstar/components/Table';
import {API} from "aws-amplify";
import {StatusIndicator} from "aws-northstar";
import Utils from '../../Utils'

const apiName = 'ekycApi'

const VerificationRequestHistory = () => {

    const [data, setData] = useState([])


    useEffect(() => {
        const fetchHistory = async () => {

            const response = await API.get(apiName, '/api/history', {})

            setData(response)
        }
        fetchHistory()
    }, [])

    // interface DataType {
    //     sessionId: string;
    //     client: string;
    //     isSuccessful: boolean
    //     documentType: string
    //     error: string
    //     time: number
    // }


    const columnDefinitions: Column<any>[] = [
        {
            id: 'sessionId',
            width: 200,
            Header: 'Id',
            accessor: 'sessionId',
        },
        {
            id: 'client',
            width: 400,
            Header: 'Client',
            accessor: 'client'
        },
        {
            id: 'isSuccessful',
            width: 200,
            Header: 'Successful',
            accessor: 'isSuccessful',
            Cell: ({row}) => {
                if (row && row.original) {
                    if (row.original.isSuccessful)
                        return <StatusIndicator statusType='positive'>Successful</StatusIndicator>;
                    else
                        return <StatusIndicator statusType='negative'>Not Successful</StatusIndicator>;

                }
                return null;
            }
        },
        {
            id: 'startTime',
            width: 200,
            Header: 'Started At',
            accessor: row => Utils.getDateTimeString(row.time)
        },
        {
            id: 'documentType',
            width: 200,
            Header: 'Document Type',
            accessor: 'documentType'
        },
        {
            id: 'error',
            width: 400,
            Header: 'Error',
            accessor: 'error'
        }

    ]

    return (

        <div>

            <Table
                tableTitle='Verification History'
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

        </div>)
}

export default VerificationRequestHistory
