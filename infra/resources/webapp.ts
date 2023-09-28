import * as core from "aws-cdk-lib/core";
import {CustomResource} from "aws-cdk-lib/core";
import {Bucket} from "aws-cdk-lib/aws-s3";
import * as s3Deployment from "aws-cdk-lib/aws-s3-deployment";
import * as cloudfront from "aws-cdk-lib/aws-cloudfront";
import * as lambda from "aws-cdk-lib/aws-lambda"
import {Code, Runtime} from "aws-cdk-lib/aws-lambda"
import {Construct} from "constructs";
import {LambdaRestApi} from "aws-cdk-lib/aws-apigateway";
import {UserPool, UserPoolClient} from "aws-cdk-lib/aws-cognito";

import {Duration} from "aws-cdk-lib";
import {Effect, PolicyStatement} from "aws-cdk-lib/aws-iam";
import {Provider} from "aws-cdk-lib/custom-resources";
import * as path from "path";


export interface StaticWebsiteConfiguration {
    /**
     * The configuration will be written to a javascript file at the supplied key. eg: runtime-configuration.js. You will
     * need to include this in a script tag in your index.html, eg: <script src="/runtime-configuration.js" />
     */
    outputS3Key: string;
    /**
     * Configuration will be injected into the global 'window' object under the given key, eg: 'runtimeConfig' means
     * you can access your configuration at window.runtimeConfig
     */
    windowProperty: string;
    /**
     * The config to make available in the window
     */
    config: { [key: string]: string };
}

interface WebAppConstructProps {
    readonly webBucket: Bucket;

    readonly configuration?: StaticWebsiteConfiguration;

    readonly userPool: UserPool;

    readonly userPoolClient: UserPoolClient;

    readonly api: LambdaRestApi;
}

export default class WebAppConstruct extends Construct {


    cfWebV2: cloudfront.CloudFrontWebDistribution

    constructor(scope: Construct, id: string, props: WebAppConstructProps) {
        super(scope, "web-app");

        //   this.createWAF(this.node.addr)


        const oai = new cloudfront.OriginAccessIdentity(this, "OAI", {
            comment: "OAI for Web Interface",
        });

        props.webBucket.grantRead(oai);


        // const webAclv2 = new CloudFrontWebAcl(this, 'WebACLV2', {
        //     name: `${this.node.addr}-WebAclV2`,
        //     managedRules: [{VendorName: 'AWS', Name: 'AWSManagedRulesCommonRuleSet'}],
        // });


        this.cfWebV2 = new cloudfront.CloudFrontWebDistribution(
            this,
            "js-web-distributionv2",
            {
                errorConfigurations: [
                    {
                        errorCode: 404,
                        responseCode: 200,
                        responsePagePath: "/index.html",
                    },
                ],
                originConfigs: [
                    {
                        s3OriginSource: {
                            originPath: "/webv2",
                            s3BucketSource: props.webBucket,
                            originAccessIdentity: oai,
                        },
                        behaviors: [{isDefaultBehavior: true}],
                    },
                ],
                //webACLId: webAclv2.getArn(Stack.of(this).account),
            },
        );


        new core.CfnOutput(this, "Web-CloudFrontUrlV2", {
            value: this.cfWebV2.distributionDomainName,
        });

        new core.CfnOutput(this, "Web-CloudFrontDistributionIdV2", {
            value: this.cfWebV2.distributionId,
        });

        const deploymentV2 = new s3Deployment.BucketDeployment(
            this,
            "deployJSsiteV2",
            {
                sources: [s3Deployment.Source.asset("../packages/webv2/build")],
                destinationBucket: props.webBucket,
                distribution: this.cfWebV2,
                destinationKeyPrefix: "webv2",
            }
        )
        if (props.configuration) {
            const uploadWebsiteConfigFunction = new lambda.Function(
                this,
                `UploadWebsiteConfigFunction`,
                {
                    runtime: Runtime.PYTHON_3_7,
                    handler: "app.on_event",
                    code: Code.fromAsset(
                        path.resolve(__dirname, "upload-website-config-handler")
                    ),
                    timeout: Duration.seconds(30),
                    initialPolicy: [
                        new PolicyStatement({
                            effect: Effect.ALLOW,
                            actions: [
                                "cloudfront:GetInvalidation",
                                "cloudfront:CreateInvalidation",
                            ],
                            resources: ["*"],
                        }),
                    ],
                }
            );

            props.webBucket.grantWrite(uploadWebsiteConfigFunction);

            const uploadWebsiteConfigProvider = new Provider(
                this,
                `UploadWebsiteConfigProvider`,
                {
                    onEventHandler: uploadWebsiteConfigFunction,
                }
            );

            // const websiteConfiguration = `window['${
            //     props.configuration.windowProperty
            // }'] = ${JSON.stringify(props.configuration.config, null, 2)};`;

            const websiteConfiguration = JSON.stringify(props.configuration.config, null, 2)


            const uploadWebsiteConfigResourcev2 = new CustomResource(
                this,
                `UploadWebsiteConfigResourcev2`,
                {
                    serviceToken: uploadWebsiteConfigProvider.serviceToken,
                    // Pass the mapping file attributes as a property. Every time the mapping file changes, the custom resource will be updated which will trigger the corresponding Lambda.
                    properties: {
                        S3_BUCKET: props.webBucket.bucketName,
                        S3_CONFIG_FILE_KEY: "webv2/" + props.configuration.outputS3Key,
                        WEBSITE_CONFIG: websiteConfiguration,
                        CLOUDFRONT_DISTRIBUTION_ID:
                        this.cfWebV2.distributionId,
                        // The bucket deployment clears the s3 bucket, so we must always run the custom resource to write the config
                        ALWAYS_UPDATE: new Date().toISOString(),
                    },
                }
            );

            uploadWebsiteConfigResourcev2.node.addDependency(deploymentV2);

        }
    }

}
