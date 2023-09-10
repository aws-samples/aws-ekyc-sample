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

import ekycApiConstruct from "../resources/api";
import StorageConstruct, * as storage from "../resources/storage";
import {IdentityConstructs} from "../resources/identity";
import WebAppConstruct from "../resources/webapp"
import {ParamStoreConstruct} from "../resources/param-store";
import SnsConstruct from "../resources/sns";
import WorkteamConstruct from "../resources/workteam";
import EventConstructs from "../resources/events";
import {CfnOutput, Stack, StackProps} from "aws-cdk-lib";
import {Construct} from "constructs";

export class EkycInfraStack extends Stack {


    constructor(scope: Construct, id: string, props?: StackProps) {
        super(scope, id, props)


        // The code that defines your stack goes here

        const storage = new StorageConstruct(this, "storage");

        //  const network = new NetworkConstruct(this, `network`)

        const identity = new IdentityConstructs(this, "identity", {
            trainingBucket: storage.trainingBucket
        })


        const topics = new SnsConstruct(this, 'topics', {groundTruthRole: identity.groundTruthRole})

        const workteams = new WorkteamConstruct(this, "workteams", {
            labellersTopic: topics.labellersTopic,
            trainingBucket: storage.trainingBucket
        })

        const param_store = new ParamStoreConstruct(this, "parameters")

        const api = new ekycApiConstruct(this, "ekyc-api", {
            trainingTable: storage.trainingTable,
            storageBucket: storage.storageBucket,
            trainingBucket: storage.trainingBucket,
            sessionsTable: storage.sessionsTable,
            verificationHistoryTable: storage.verificationHistoryTable,
            cognitoUserPool: identity.userPool,
            cognitoAppClient: identity.userPoolClient,
            dataRequestsTable: storage.dataRequestsTable,
            approvalsTopic: topics.approvalTopic,
            RekognitionCustomLabelsProjectArnParameter: param_store.rekognitionCustomLabelsProjectArn,
            RekognitionCustomLabelsProjectVersionArnParameter: param_store.rekognitionCustomLabelsProjectVersionArn,
            workTeam: workteams.labellersWorkTeam,
            groundTruthRole: identity.groundTruthRole,
            useFieldCoordinatesExtractionMethodParameter: param_store.useFieldCoordinatesExtractionMethod
        })

        new WebAppConstruct(this, "js-web-app", {
            webBucket: storage.webBucket,
            userPool: identity.userPool,
            userPoolClient: identity.userPoolClient,
            api: api.api,
            configuration: {
                outputS3Key: "runtime-config.json",
                windowProperty: "runtimeConfig",
                config: {
                    region: this.region,
                    apiStage: api.api.url,
                    cognitoUserPoolId: identity.userPool.userPoolId,
                    cognitoAppClientId: identity.userPoolClient.userPoolClientId,
                    cognitoDomain: identity.userPoolDomain.domainName,
                    identityPoolId: identity.identityPool.ref,
                },
            }
        })

        // new OcrServiceConstruct(this, 'ocr-service', {
        //     vpc: network.vpc,
        //     ecsRole: identity.ecsRole
        // })


        new EventConstructs(this, 'event-triggers',
            {
                RekognitionCustomLabelsProjectArnParameter: param_store.rekognitionCustomLabelsProjectArn,
                RekognitionCustomLabelsProjectVersionArnParameter: param_store.rekognitionCustomLabelsProjectVersionArn,
                trainingTable: storage.trainingTable,
                trainingBucket: storage.trainingBucket
            })

        new CfnOutput(this, "DeploymentRegion", {
            value: Stack.of(this).region,
            description: "The region that this stack has been deployed in.",
            exportName: "deploymentRegion",
        });
    }
}
