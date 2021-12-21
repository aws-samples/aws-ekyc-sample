import fs from 'fs'
import path from 'path'
import cdkOutput from '../output.json'

interface IConfig {
    region: string,
    mandatorySignIn: boolean,
    userPoolId?: string,
    userPoolWebClientId?: string,
    authenticationFlowType: string
    dataApiEndpoint?: string
}

export default class AmplifyConfigWriter  {

    public execute = async () => {
        console.log(`Starting Amplify config writer`)
        await this.writeConfig()
    }

    private writeConfig = async () => {

        console.log('Reading configuration from CDK output')

        const configOutput: IConfig = {
            region: "ap-southeast-1",
            authenticationFlowType: "USER_PASSWORD_AUTH",
            mandatorySignIn: true
        }

        for (const [key, value] of Object.entries(cdkOutput.EkycInfraStack)) {
            if (key.startsWith('identityUserPoolClient'))
                configOutput.userPoolWebClientId = value
            else if (key.startsWith('identityUserPool'))
                configOutput.userPoolId = value
            else if (key.startsWith('ekycapiekycdataapi'))
                configOutput.dataApiEndpoint = value
        }

        const absPath = path.resolve('../ui/src/config.json')

        console.log(`Writing to Amplify config file ${absPath}`)

        const strOutput = JSON.stringify(configOutput)

        console.log(`JSON output: ${strOutput}`)

        await fs.writeFileSync(absPath, strOutput)

    }

}
