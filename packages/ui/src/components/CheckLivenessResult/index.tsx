// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
import React, {useEffect, useState} from "react";
import {API} from "aws-amplify";
import {Container, Stack} from "aws-northstar";
import Button from "aws-northstar/components/Button";
import Alert, {AlertType} from 'aws-northstar/components/Alert';
import LoadingIndicator from "aws-northstar/components/LoadingIndicator";
import {ICreateSessionResponse} from '../EkycSession'

const apiName = 'ekycApi'

export interface CheckLivenessResultProps {
    session: ICreateSessionResponse
    onRestart: () => void
}

const CheckLivenessResult = (props: CheckLivenessResultProps) => {

    const [isLoading, setIsLoading] = useState(true)

    const [visible, setVisible] = useState(false);

    const [alertType, setAlertType] = useState<AlertType>()

    const [title, setTitle] = useState("")

    const [msg, setMsg] = useState("")

    useEffect(() => {
        checkLiveness()
    }, [])

    const checkLiveness = (() => {

        setVisible(true)

        setIsLoading(true)


        API.get(apiName, '/api/liveness/verify', {
            queryStringParameters: {
                sessionId: props.session.id
            }
        }).then((response) => {

            if (response.isLive) {
                setTitle("Liveness check passed")
                setAlertType("success")
                setMsg(`Document liveness verified for session ${props.session.id}`)
            } else {
                setTitle("Liveness check failed")
                setAlertType("warning")
                setMsg(response.error)
            }
        })
            .catch(err => {

                console.log(err.response.data.error)
                if (err.response)
                    setMsg(err.response.data.error)
                else
                    setMsg('An error occurred checking liveness.')

            })
            .then(() => {
                setIsLoading(false)

            })
    })


    return (
        <>

            <Container headingVariant='h4' title={title}>
                <Stack spacing='s'>
                    <Alert type={alertType}>{msg}</Alert>
                    <div style={{display: isLoading ? 'block' : 'none'}}>
                        <LoadingIndicator label='Checking....'/>
                    </div>
                    <div style={{display: isLoading ? 'none' : 'block'}}>
                        <Stack spacing='s'>
                            <Button onClick={checkLiveness}>Check Liveness</Button>

                            <Button onClick={props.onRestart}>Start Over</Button>
                        </Stack>
                    </div>
                </Stack>
            </Container>

        </>
    )

}

export default CheckLivenessResult
