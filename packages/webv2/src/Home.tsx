/*********************************************************************************************************************
 Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

 Licensed under the Apache License, Version 2.0 (the "License").
 You may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 ******************************************************************************************************************** */

import {Container, Header, SpaceBetween} from '@cloudscape-design/components';
import {useContext, useEffect} from 'react';
import {AppLayoutContext} from './App';

/**
 * Component to render the home "/" route.
 */
const Home: React.FC = () => {
    const {appLayoutProps, setAppLayoutProps} = useContext(AppLayoutContext);

    useEffect(() => {
        setAppLayoutProps({
            ...appLayoutProps,
            contentHeader: <Header>AWS eKYC</Header>,
            contentType: "default",
        });
    }, [setAppLayoutProps]);

    return (
        <SpaceBetween size="l">
            <Container>
                The AWS eKYC solution helps you extract and verify documents and user liveness.
            </Container>
        </SpaceBetween>
    );
};

export default Home;