import {SageMakerClient, DescribeWorkforceCommand, DescribeWorkforceCommandInput} from "@aws-sdk/client-sagemaker";
import {
    CognitoIdentityProviderClient,
    DescribeUserPoolClientCommand,
    DescribeUserPoolClientRequest,
    ListUserPoolClientsCommand,
    ListUserPoolClientsCommandInput,
    ListUserPoolsCommand,
    ListUserPoolsCommandInput, UpdateUserPoolClientCommand, UpdateUserPoolClientCommandInput,
    UpdateUserPoolClientRequest,
    UserPoolClientDescription,
    UserPoolClientType,
    UserPoolDescriptionType
} from "@aws-sdk/client-cognito-identity-provider";

export default class GroundTruthCognitoSync {

    private readonly sageMakerClient: SageMakerClient
    private readonly cognitoIdentityClient: CognitoIdentityProviderClient

    constructor() {
        this.sageMakerClient = new SageMakerClient({})
        this.cognitoIdentityClient = new CognitoIdentityProviderClient({})
    }

    public execute = async () => {

        console.log("Syncing Amazon SageMaker Ground Truth Private Workforce URLs with Cognito User Pool Client");

        const subdomains = await this.getSubdomains()

        const userPool = await this.findUserPool()

        if (!userPool.Id)
            throw new Error(`User pool name is blank.`)

        const userPoolClient = await this.findUserPoolClient(userPool?.Id)

        await this.updateUserPoolClient(subdomains, userPoolClient)
    }

    private updateUserPoolClient = async (subdomains: string[], userPoolClient: UserPoolClientType): Promise<void> => {

        const updateInput = {} as UpdateUserPoolClientCommandInput

        Object.assign<UpdateUserPoolClientCommandInput, UserPoolClientType>(updateInput, userPoolClient)

        console.log(`Updating Cognito User Pool Client with Subdomains: ${subdomains}`);

        await this.cognitoIdentityClient.send(new UpdateUserPoolClientCommand(updateInput))
    }

    private findUserPoolClient = async (userPoolId: string): Promise<UserPoolClientType> => {
        const listUserPoolClientInput: ListUserPoolClientsCommandInput = {UserPoolId: userPoolId, MaxResults: 10}

        const response = await this.cognitoIdentityClient.send(new ListUserPoolClientsCommand(listUserPoolClientInput))

        if (!response || !response.UserPoolClients)
            throw new Error(`User pool client not found.`)

        const userPoolClient = response.UserPoolClients.find(a => a.ClientName?.startsWith('identitylabellersclient'))

        if (!userPoolClient)
            throw new Error(`User pool client not found.`)

        const describeRequest: DescribeUserPoolClientRequest = {
            UserPoolId: userPoolId,
            ClientId: userPoolClient.ClientId
        }

        const describeResponse = await this.cognitoIdentityClient.send(new DescribeUserPoolClientCommand(describeRequest))

        if (!describeResponse.UserPoolClient)
            throw new Error(`User pool client not found.`)

        return describeResponse.UserPoolClient
    }

    private findUserPool = async (): Promise<UserPoolDescriptionType> => {

        const cognitoInput: ListUserPoolsCommandInput = {MaxResults: 10}

        const response = await this.cognitoIdentityClient.send(new ListUserPoolsCommand(cognitoInput))

        if (response.UserPools?.length === 0)
            throw new Error(`Unable to find the user pool to link.`)

        const userPool = response?.UserPools?.find(u => u.Name === 'ekyc-user-pool')

        if (!userPool)
            throw new Error(`Could not find the eKYC user pool.`)

        return userPool
    }

    private getSubdomains = async (): Promise<string[]> => {


        const request: DescribeWorkforceCommandInput = {WorkforceName: 'default'}

        const response = await this.sageMakerClient.send(new DescribeWorkforceCommand(request))

        return [
            `https://${response.Workforce?.SubDomain}/oauth2/idpresponse`,
            `https://${response.Workforce?.SubDomain}`]

    }

}





