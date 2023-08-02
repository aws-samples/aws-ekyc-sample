import React, {useState} from 'react';
import {MemoryRouter, Route, Switch} from 'react-router';
import AppLayout from 'aws-northstar/layouts/AppLayout';
import Box from 'aws-northstar/layouts/Box';
import Header from 'aws-northstar/components/Header';
import {SideNavigationItemType} from 'aws-northstar/components/SideNavigation';
import HelpPanel from 'aws-northstar/components/HelpPanel';
import Link from 'aws-northstar/components/Link';
import Text from 'aws-northstar/components/Text';
import Heading from 'aws-northstar/components/Heading';
import EkycSession from "../EkycSession";
import TrainingJobsTable from "../TrainingJobs"
import VerificationRequestHistory from "../VerificationRequestHistory";
import FieldData from '../FieldData';
import {Auth} from "aws-amplify";
import NewTrainingJob from '../NewTrainingJob'
import Button from "aws-northstar/components/Button";


function MainContent() {

    const header = <Header title='AWS eKYC Demo'/>;
    const navigationItems = [
        {type: SideNavigationItemType.LINK, text: 'Home', href: '/'},
        {type: SideNavigationItemType.LINK, text: 'New Session', href: '/session'},
        {type: SideNavigationItemType.LINK, text: 'Get Field Data', href: '/fields'},
        {type: SideNavigationItemType.LINK, text: 'Verification History', href: '/history'},
        {type: SideNavigationItemType.LINK, text: 'Training Jobs', href: '/training'},
        {type: SideNavigationItemType.LINK, text: 'New Training Job', href: '/newjob'},
        {type: SideNavigationItemType.DIVIDER},
        {
            type: SideNavigationItemType.LINK,
            text: 'User License',
            href: 'https://aws.amazon.com/asl/'
        },
        {
            type: SideNavigationItemType.LINK,
            text: 'AWS Website',
            href: 'https://aws.amazon.com',
        }
    ];

    const navigation = (
        <div>
            {
                navigationItems.map(ni=>{
                    return <div key={ni.text}><a href={ni.href}>{ni.text}</a></div>
                })
            }
        {/*<SideNavigation*/}
        {/*    header={{*/}
        {/*        href: '/',*/}
        {/*        text: 'eKYC Test Interface',*/}
        {/*    }}*/}
        {/*    items={navigationItems}*/}
        {/*/>*/}
        </div>
    );
    const helpPanel = (
        <HelpPanel
            header="Help panel title (h2)"
            learnMoreFooter={[
                <Link key='internalDoc' href="/docs">Link to internal documentation</Link>,
                <Link key='externalDoc' href="https://www.yoursite.com">Link to external documentation</Link>,
            ]}
        >
            <Text variant="p">
                This is a paragraph with some <b>bold text</b> and also some <i>italic text.</i>
            </Text>
            <Heading variant="h4">h4 section header</Heading>
            <Heading variant="h5">h5 section header</Heading>
        </HelpPanel>
    );
    /*const breadcrumbGroup = (
        <BreadcrumbGroup
            items={[
                {
                    text: 'Home',
                    href: '#home',
                },
                {
                    text: 'Path1',
                    href: '#path1',
                },
                {
                    text: 'Path2',
                    href: '#path2',
                },
                {
                    text: 'Path3',
                    href: '#path3',
                }
            ]}
        />
    );*/
    const defaultNotifications = [
        {
            id: '1',
            header: 'Successfully updated 4 orders',
            type: 'success',
            content: 'This is a success flash message.',
            dismissible: true,
        },
        {
            id: '2',
            header: 'Failed to update 1 order',
            type: 'error',
            content: 'This is a dismissible error message with a button.',
            buttonText: 'Retry',
            onButtonClick: () => console.log('Button clicked'),
            dismissible: true,
        },
        {
            id: '3',
            header: 'Warning',
            type: 'warning',
            content: 'This is warning content',
            dismissible: true,
        }
    ];

    const mainContent = (
        <Box bgcolor="grey.300" width="100%" height="1000px">
            Welcome to the AWS eKYC Demo App
        </Box>
    );

    const [notifications, setNotifications] = useState(defaultNotifications);

    const handleDismiss = (id: any) => {
        setNotifications(notifications.filter(n => n.id !== id));
    };

    const handleLogout = async () => {
        try {
            await Auth.signOut();
        } catch (error) {
            console.log('error signing out: ', error);
        }
    }

    return (
        <MemoryRouter>

            <AppLayout
                header={header}
                navigation={navigation}
                helpPanel={helpPanel}>

                <Switch>
                    <Route path="/" >
                        {mainContent}
                    </Route>
                    <Route path="/session">{EkycSession}</Route>
                    <Route path="/history">{VerificationRequestHistory}</Route>
                    <Route path="/training">{TrainingJobsTable}</Route>
                    <Route path="/fields" >{FieldData}</Route>
                    <Route path="/newjob">{NewTrainingJob}</Route>
                    <Route path="/logout"><>
                        <Button onClick={handleLogout}>Log Out</Button>
                    </></Route>

                </Switch>
            </AppLayout>
        </MemoryRouter>
    )
}

export default MainContent
