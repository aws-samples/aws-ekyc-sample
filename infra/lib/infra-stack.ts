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

        const jsWebApp = new WebAppConstruct(this, "js-web-app", {
            webBucket: storage.webBucket
        })

        const identity = new IdentityConstructs(this, "identity", {
            cfJsWebApp: jsWebApp.cfWeb,
            trainingBucket: storage.trainingBucket
        })

        const topics = new SnsConstruct(this, 'topics', {groundTruthRole: identity.groundTruthRole})

        const workteams = new WorkteamConstruct(this, "workteams", {
            cognitoUserPool: identity.userPool,
            cognitoAppClient: identity.labellersClient,
            labellersTopic: topics.labellersTopic,
            labellersGroup: identity.labellersGroup,
            trainingBucket: storage.trainingBucket
        })

        const param_store = new ParamStoreConstruct(this, "parameters")

         new ekycApiConstruct(this, "ekyc-api", {
            trainingTable: storage.trainingTable,
            storageBucket: storage.storageBucket,
            trainingBucket: storage.trainingBucket,
            sessionsTable: storage.sessionsTable,
            verificationHistoryTable: storage.verificationHistoryTable,
            cognitoUserPool: identity.userPool,
            cognitoAppClient: identity.userPoolClient,
            dataRequestsTable: storage.dataRequestsTable,
            approvalsTopic: topics.approvalTopic,
            RekognitionCustomLabelsProjectArnParameter:param_store.rekognitionCustomLabelsProjectArn,
            RekognitionCustomLabelsProjectVersionArnParameter: param_store.rekognitionCustomLabelsProjectVersionArn,
            jsCloudFrontDistribution: jsWebApp.cfWeb,
            workTeam: workteams.labellersWorkTeam,
            groundTruthRole: identity.groundTruthRole,
            useFieldCoordinatesExtractionMethodParameter:param_store.useFieldCoordinatesExtractionMethod
        })


       new EventConstructs(this, 'event-triggers',
            {
                RekognitionCustomLabelsProjectArnParameter:param_store.rekognitionCustomLabelsProjectArn,
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
