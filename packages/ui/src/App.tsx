/*
Copyright 2021 Amazon.com, Inc. and its affiliates. All Rights Reserved.

Licensed under the Amazon Software License (the "License").
You may not use this file except in compliance with the License.
A copy of the License is located at

  http://aws.amazon.com/asl/

or in the "license" file accompanying this file. This file is distributed
on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
express or implied. See the License for the specific language governing
permissions and limitations under the License.
 */
import './App.css';
import MainContent from './components/MainContent'
import NorthStarThemeProvider from 'aws-northstar/components/NorthStarThemeProvider'
import {AmplifySignOut, withAuthenticator} from '@aws-amplify/ui-react';
import Amplify, {Auth} from 'aws-amplify';
import React from 'react';
import config from './config.json'

document.title = 'eKYC demo'

Amplify.configure({
    // OPTIONAL - if your API requires authentication
    Auth: {
        region:'ap-southeast-1',
        mandatorySignIn:true,
        userPoolId: config.userPoolId,
        userPoolWebClientId: config.userPoolWebClientId,
        authenticationFlowType: config.authenticationFlowType,
    },
    API: {
        endpoints: [
            {
                name: "ekycApi",
                endpoint: "https://demjt2pum7.execute-api.ap-southeast-1.amazonaws.com/prod",
                //  endpoint: "https://localhost:5001",
                region: 'ap-southeast-1',
                custom_header: async () => {
                    return {Authorization: `Bearer ${(await Auth.currentSession()).getIdToken().getJwtToken()}`}
                }
            }
        ]
    }
});

function App() {
    return (
        <div>
            <NorthStarThemeProvider>
                <AmplifySignOut/>
                <MainContent/>
            </NorthStarThemeProvider>

        </div>
    );
}


export default withAuthenticator(App);
//export default App;
