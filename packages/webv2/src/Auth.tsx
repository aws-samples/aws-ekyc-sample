/**********************************************************************************************************************
 *  Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.                                                *
 *                                                                                                                    *
 *  Licensed under the Amazon Software License (the "License"). You may not use this file except in compliance        *
 *  with the License. A copy of the License is located at                                                             *
 *                                                                                                                    *
 *     https://aws.amazon.com/asl/                                                                                    *
 *                                                                                                                    *
 *  or in the "license" file accompanying this file. This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES *
 *  OR CONDITIONS OF ANY KIND, express or implied. See the License for the specific language governing permissions    *
 *  and limitations under the License.                                                                                *
 **********************************************************************************************************************/

import {Amplify} from "aws-amplify";
import {fetchAuthSession, getCurrentUser, fetchUserAttributes} from "aws-amplify/auth";
import {Hub} from "aws-amplify/utils";
import {Authenticator, Theme, ThemeProvider, useTheme} from "@aws-amplify/ui-react";
import React, {createContext, useCallback, useEffect, useMemo, useState} from "react";
import {configureApiClient} from "./apiClient";

/**
 * Context for storing the runtimeContext.
 */
export const RuntimeConfigContext = createContext<any>({});

/**
 * Sets up the runtimeContext and Cognito auth.
 *
 * This assumes a runtime-config.json file is present at '/'. In order for Auth to be set up automatically,
 * the runtime-config.json must have the following properties configured: [region, userPoolId, userPoolWebClientId, identityPoolId].
 */
const Auth: React.FC<any> = ({children}) => {
    const [runtimeContext, setRuntimeContext] = useState<any>(undefined);
    const {tokens} = useTheme();

    // Customize your login theme
    const theme: Theme = useMemo(
        () => ({
            name: "AuthTheme",
            tokens: {
                components: {
                    passwordfield: {
                        button: {
                            _hover: {
                                backgroundColor: {
                                    value: "white",
                                },
                                borderColor: {
                                    value: tokens.colors.blue["40"].value,
                                },
                            },
                        },
                    },
                },
                colors: {
                    background: {
                        primary: {
                            value: tokens.colors.neutral["20"].value,
                        },
                        secondary: {
                            value: tokens.colors.neutral["100"].value,
                        },
                    },
                    brand: {
                        primary: {
                            10: tokens.colors.blue["20"],
                            80: tokens.colors.blue["40"],
                            90: tokens.colors.blue["40"],
                            100: tokens.colors.blue["40"],
                        },
                    },
                },
            },
        }),
        [tokens]
    );

    useEffect(() => {
        fetch("/runtime-config.json")
            .then((response) => {
                return response.json();
            })
            .then((runtimeCtx) => {
                if (runtimeCtx.apiStage && runtimeCtx.region && runtimeCtx.userPoolId && runtimeCtx.userPoolWebClientId && runtimeCtx.identityPoolId) {
                    Amplify.configure({
                        Auth: {
                            Cognito: {
                                userPoolId: runtimeCtx.userPoolId,
                                userPoolClientId: runtimeCtx.userPoolWebClientId,
                                identityPoolId: runtimeCtx.identityPoolId,
                            }
                        }
                    });
                    configureApiClient(runtimeCtx.apiStage);
                    setRuntimeContext(runtimeCtx);
                    Promise.all([getCurrentUser(), fetchUserAttributes()])
                        .then(([currentUser, attributes]) => {
                            setRuntimeContext({...runtimeCtx, user: {username: currentUser.username, attributes}});
                        })
                        .catch(() => {});
                } else {
                    console.warn("runtime-config.json should have region, userPoolId, userPoolWebClientId & identityPoolId.");
                }
            })
            .catch(() => {
                console.warn("unable to load runtime-config.json from public directory");
                setRuntimeContext({});
            });
    }, [setRuntimeContext]);

    useEffect(() => {
        const unsubscribe = Hub.listen("auth", (data) => {
            switch (data.payload.event) {
                case "signedIn":
                    Promise.all([getCurrentUser(), fetchUserAttributes()])
                        .then(([currentUser, attributes]) => {
                            setRuntimeContext((prevRuntimeContext: any) => ({
                                ...prevRuntimeContext,
                                user: {username: currentUser.username, attributes},
                            }));
                        })
                        .catch((e) => console.error(e));
                    break;
                case "signedOut":
                    window.location.reload();
                    break;
            }
        });
        return () => unsubscribe();
    }, []);

    const AuthWrapper: React.FC<any> = useCallback(
        ({children: _children}) =>
            runtimeContext?.userPoolId ? (
                <ThemeProvider theme={theme}>
                    <Authenticator variation="modal" hideSignUp>
                        {_children}
                    </Authenticator>
                </ThemeProvider>
            ) : (
                <>
                    {
                        runtimeContext ? _children : <></> // Don't render anything if the context has not finalized
                    }
                </>
            ),
        [runtimeContext, theme]
    );

    return (
        <AuthWrapper>
            <RuntimeConfigContext.Provider value={{runtimeContext, setRuntimeContext}}>
                {children}
            </RuntimeConfigContext.Provider>
        </AuthWrapper>
    );
};

export default Auth;
