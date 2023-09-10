import localConfig from "../../local-config.json";

export interface AppConfig {
    apiAddress: string;
    webSocketUrl?: string;
    region: string;
    userPoolId: string;
    userPoolWebClientId: string;
    cognitoDomain: string;
    identityPoolId: string;
}

const getConfig = async (): Promise<AppConfig> => {
    if (
        window.location.hostname === "localhost" ||
        window.location.hostname === "127.0.0.1"
    ) {
        const local = {
            region: localConfig.region,
            userPoolId: localConfig.userPoolId,
            apiAddress: "http://localhost:5000",
            userPoolWebClientId: localConfig.userPoolWebClientId,
            cognitoDomain: localConfig.cognitoDomain,
            identityPoolId: localConfig.identityPoolId
        };

        return local;
    }


    return {
        region: (window as any)["runtimeConfig"].region,
        userPoolId: (window as any)["runtimeConfig"].cognitoUserPoolId,
        userPoolWebClientId: (window as any)["runtimeConfig"].cognitoAppClientId,
        cognitoDomain: (window as any)["runtimeConfig"].cognitoDomain,
        identityPoolId: (window as any)["runtimeConfig"].identityPoolId,
        // apiAddress:"http://localhost:9200"
        apiAddress: (window as any)["runtimeConfig"].apiStage
    };

};

export default getConfig;
