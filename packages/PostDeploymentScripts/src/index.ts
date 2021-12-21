import AmplifyConfigWriter from "./AmplifyConfigWriter";
import GroundTruthCognitoSync from "./GroundTruthCognitoSync";

async function wrapper() {


    const configWriter = new AmplifyConfigWriter().execute()

    const groundTruthCognitoSync = new GroundTruthCognitoSync().execute()

    Promise
        .all([configWriter, groundTruthCognitoSync])
        .then(() => console.log('All post-deployment tasks completed.'))
        .catch((err) => `Error - ${err}`)


}


wrapper()
