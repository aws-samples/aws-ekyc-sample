import * as core from "aws-cdk-lib/core";
import {Bucket} from "aws-cdk-lib/aws-s3";
import * as s3Deployment from "aws-cdk-lib/aws-s3-deployment";
import * as cloudfront from "aws-cdk-lib/aws-cloudfront";
import CloudFrontWebAcl from './cloudfront-web-acl'
import {Construct} from "constructs";

interface WebAppConstructProps {
    readonly webBucket: Bucket;
}

export default class WebAppConstruct extends Construct {

    cfWeb: cloudfront.CloudFrontWebDistribution


    constructor(scope: Construct, id: string, props: WebAppConstructProps) {
        super(scope, "web-app");

        //   this.createWAF(this.node.addr)


        const oai = new cloudfront.OriginAccessIdentity(this, "OAI", {
            comment: "OAI for Web Interface",
        });

        props.webBucket.grantRead(oai);


        const webAcl = new CloudFrontWebAcl(this, 'WebACL', {
            name:  `${this.node.addr}-WebAcl`,
            managedRules: [{ VendorName: 'AWS', Name: 'AWSManagedRulesCommonRuleSet' }],
          });

        this.cfWeb = new cloudfront.CloudFrontWebDistribution(
            this,
            "js-web-distribution",
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
                            originPath: "/webui",
                            s3BucketSource: props.webBucket,
                            originAccessIdentity: oai,
                        },
                        behaviors: [{isDefaultBehavior: true}],
                    },
                ],
                webACLId :webAcl.getArn(core.Stack.of(this).account),
            },
        );


        new core.CfnOutput(this, "JS-CloudFrontUrl", {
            value: this.cfWeb.distributionDomainName,
        });

        new core.CfnOutput(this, "JS-CloudFrontDistributionId", {
            value: this.cfWeb.distributionId,
        });

        const deployment = new s3Deployment.BucketDeployment(
            this,
            "deployJSsite",
            {
                sources: [s3Deployment.Source.asset("../packages/ui/build")],
                destinationBucket: props.webBucket,
                distribution: this.cfWeb,
                destinationKeyPrefix: "webui",
            }
        )


    }

}
