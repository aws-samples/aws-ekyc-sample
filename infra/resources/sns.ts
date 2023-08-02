import {Construct} from "constructs";
import * as iam from "aws-cdk-lib/aws-iam"
import {ServicePrincipal} from "aws-cdk-lib/aws-iam"
import * as sns from "aws-cdk-lib/aws-sns"
import {CfnOutput} from "aws-cdk-lib";

interface SnsProps {
    groundTruthRole: iam.Role
}

export default class SnsConstruct extends Construct {

    readonly approvalTopic: sns.Topic

    // Topic for labelling of Ground Truth images
    readonly labellersTopic: sns.Topic

    constructor(scope: Construct, id: string, props: SnsProps) {

        super(scope, id)


        this.approvalTopic = new sns.Topic(this, 'ekyc-approval-topic', {
            displayName: 'eKYC Approval Topic',
        })

        //this.approvalTopic.addSubscription(new subscriptions.EmailSubscription('youremail@domain.com'))

        new CfnOutput(this, "approvalTopic", {
            value: this.approvalTopic.topicArn
        });

        this.labellersTopic = new sns.Topic(this, 'ekyc-labelling-topic', {
            displayName: 'eKYC Labelling Topic',
        })

        // this.labellersTopic.addSubscription(new subscriptions.EmailSubscription('youremail@domain.com'))

        new CfnOutput(this, "labellingTopic", {
            value: this.labellersTopic.topicArn
        });

        const policyStatementApproval = new iam.PolicyStatement({
            effect: iam.Effect.ALLOW,
            actions: ["sns:Publish"],
            principals: [new ServicePrincipal('sagemaker.amazonaws.com')],
            resources: [this.approvalTopic.topicArn]
        })

        this.approvalTopic.addToResourcePolicy(policyStatementApproval)


        const policyStatementLabellers = new iam.PolicyStatement({
            effect: iam.Effect.ALLOW,
            actions: ["sns:Publish"],
            principals: [new ServicePrincipal('sagemaker.amazonaws.com')],
            resources: [this.labellersTopic.topicArn]
        })

        this.labellersTopic.addToResourcePolicy(policyStatementLabellers)


    }

}
