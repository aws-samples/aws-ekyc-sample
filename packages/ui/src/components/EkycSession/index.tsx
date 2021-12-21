import React, {useEffect, useState} from 'react';
import SelfieTaker from '../SelfieTaker'
import EyesClosedTaker from '../EyesCloseTaker'
import NosePointTaker from '../NosePointTaker'
import {API} from "aws-amplify";
import CheckLivenessResult from '../CheckLivenessResult'
import DocumentTaker from "../DocumentImageTaker";

const apiName = 'ekycApi'

enum Stages {
    Selfie,
    EyesClose,
    NosePoint,
    ShowResult,
    Document
}

export interface ICreateSessionResponse {
    readonly id?: string,
    readonly noseBoundsTop?: number,
    readonly noseBoundsLeft?: number,
    readonly noseBoundsWidth?: number,
    readonly noseBoundsHeight?: number
}

const EkycSession = () => {

    const [session, setSession] = useState<ICreateSessionResponse>()

    const [stage, setStage] = useState(Stages.Selfie)

    const [selfieImgSrc, setSelfieImgSrc] = useState<string>()


    useEffect(() => {

        if (!session) {
            console.log('Getting a new session')
            getNewSession()
        }

    }, [])


    const onSelfieUploaded = async (imgSrc: string) => {

        setSelfieImgSrc(imgSrc)
        setStage(Stages.Document)

    }

    const onDocumentImageUploaded = async (documentType: string) => {
        setStage(Stages.NosePoint)
    }

    const onNosePointTaken = async () => {
        setStage(Stages.EyesClose)
    }

    const onEyesCloseTaken = async () => {
        setStage(Stages.ShowResult)
    }

    const onRestart = async () => {
        setStage(Stages.Selfie)
    }


    const getNewSession = async () => {

        const response = await API.post(apiName, '/api/session/new', {})
        // Parse the response
        if (response) {

            console.log(response)

            setSession(response)

            //    console.log(`Response from CreateSession: ${JSON.stringify(session)}`)

            await setStage(Stages.Selfie)

            // setSelfieProps({sessionId:response.id,onSelfieTaken:onSelfieUploaded})

        }
    }

    return (


        <div>
            {stage === Stages.Selfie &&
            <SelfieTaker sessionId={session?.id} onSelfieTaken={onSelfieUploaded}/>
            }
            {
                stage === Stages.EyesClose &&
                <EyesClosedTaker session={session} onEyesCloseTaken={onEyesCloseTaken}/>
            }
            {
                stage === Stages.Document &&
                <DocumentTaker onDocumentImageTaken={onDocumentImageUploaded} sessionId={session?.id}/>

            }
            {
                stage === Stages.NosePoint &&
                <NosePointTaker onNoisePointTaken={onNosePointTaken} session={session} selfieImgSrc={selfieImgSrc}/>
            }
            {
                stage === Stages.ShowResult &&
                <CheckLivenessResult onRestart={onRestart} session={session}/>
            }
        </div>
    );
}

export default EkycSession
