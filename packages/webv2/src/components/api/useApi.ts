import {Amplify, Auth} from 'aws-amplify';
import axios from 'axios';
import * as api from '../../apiClient';
import {DataApi} from '../../apiClient';

const getCredentials = async () => {
    const session = await Auth.currentSession();
    return {
        AccessToken: session.getAccessToken(),
        IdToken: session.getIdToken(),
    };
};

const jwtInterceptor = axios.interceptors.request.use(async (config) => {
    const {IdToken} = await getCredentials();
    const jwt = IdToken.getJwtToken();
    console.log(`Auth: Bearer ${jwt}`)
    config.headers.Authorization = `Bearer ${jwt}`

    return config;
});

const getApiEndpoint = () => {

    let apiEndpoint = 'http://localhost:9200'
    //If running locally, connect to the local API
    if (
        window.location.hostname === 'localhost' ||
        window.location.hostname === '127.0.0.1'
    ) {
        axios.interceptors.request.eject(jwtInterceptor);
    } else {
        apiEndpoint = Amplify.API.endpoints[0].endpoint
    }

    return apiEndpoint
}


export const getDataApi = async () => {

    const apiClient = new DataApi(
        new api.Configuration({
            basePath: getApiEndpoint(),
        }),
        undefined,
        axios, // Make sure we use the one we just configured
    );
    return apiClient;
};
