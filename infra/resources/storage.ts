import * as core from "@aws-cdk/core";
import {RemovalPolicy} from "@aws-cdk/core";
import * as s3 from "@aws-cdk/aws-s3";
import {BucketEncryption} from "@aws-cdk/aws-s3";
import * as dynamodb from "@aws-cdk/aws-dynamodb";
import {TableEncryption} from "@aws-cdk/aws-dynamodb";

export default class StorageConstruct extends core.Construct {
    public readonly sessionsTable: dynamodb.Table;

    public readonly verificationHistoryTable: dynamodb.Table;

    public readonly dataRequestsTable: dynamodb.Table;

    public readonly trainingTable: dynamodb.Table;

    public readonly webBucket: s3.Bucket;

    public readonly storageBucket: s3.Bucket;

    public readonly trainingBucket: s3.Bucket;

    constructor(scope: core.Construct, id: string) {
        super(scope, id);

        new s3.Bucket(this, "deployBucket", {
            versioned: false,
            removalPolicy: core.RemovalPolicy.DESTROY,
            autoDeleteObjects: true,
            encryption: BucketEncryption.S3_MANAGED,

        });

        this.storageBucket = new s3.Bucket(this, "storageBucket", {
            versioned: false,
            removalPolicy: core.RemovalPolicy.DESTROY,
            encryption: BucketEncryption.S3_MANAGED,
            enforceSSL: true,
            autoDeleteObjects: true,
            lifecycleRules: [
                {
                    abortIncompleteMultipartUploadAfter: core.Duration.days(1),
                    expiration: core.Duration.days(1),
                },
            ],
            cors: [
                {
                    allowedMethods: [s3.HttpMethods.PUT],
                    allowedOrigins: ["*"],
                    allowedHeaders: ["*"],
                },
            ],
        });

        new core.CfnOutput(this, "StorageBucket", {
            value: this.storageBucket.bucketName,
        });

        this.trainingBucket = new s3.Bucket(this, "trainingBucket", {
            versioned: false,
            enforceSSL: true,
            removalPolicy: core.RemovalPolicy.DESTROY,
            encryption: BucketEncryption.S3_MANAGED,
            autoDeleteObjects: true,
            cors: [
                {
                    allowedMethods: [
                        s3.HttpMethods.GET,
                        s3.HttpMethods.POST,
                        s3.HttpMethods.PUT,
                        s3.HttpMethods.DELETE,
                        s3.HttpMethods.HEAD,
                    ],
                    allowedOrigins: ["*"],
                    allowedHeaders: ["*"],
                },
            ],
        });

        new core.CfnOutput(this, "TrainingBucket", {
            value: this.trainingBucket.bucketName,
        });

        this.webBucket = new s3.Bucket(this, "ui-hosting-bucket", {
            removalPolicy: core.RemovalPolicy.DESTROY,
            autoDeleteObjects: true,
            websiteIndexDocument: "index.html",
            cors: [
                {
                    allowedMethods: [
                        s3.HttpMethods.GET,
                        s3.HttpMethods.POST,
                        s3.HttpMethods.PUT,
                        s3.HttpMethods.DELETE,
                        s3.HttpMethods.HEAD,
                    ],
                    allowedOrigins: ["*"],
                    allowedHeaders: ["*"],
                },
            ],
            enforceSSL:true
        });

        new core.CfnOutput(this, "WebBucket", {value: this.webBucket.bucketName});

        this.sessionsTable = new dynamodb.Table(this, "Sessions", {
            partitionKey: {name: "Id", type: dynamodb.AttributeType.STRING},
            billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
            timeToLiveAttribute: "expiry",
            encryption: TableEncryption.AWS_MANAGED,
            removalPolicy: RemovalPolicy.DESTROY
        });

        new core.CfnOutput(this, "SessionsTable", {
            value: this.sessionsTable.tableName,
        });

        this.dataRequestsTable = new dynamodb.Table(this, "DataRequests", {
            partitionKey: {name: "Id", type: dynamodb.AttributeType.STRING},
            billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
            timeToLiveAttribute: "expiry",
            encryption: TableEncryption.AWS_MANAGED,
            removalPolicy: RemovalPolicy.DESTROY
        });

        new core.CfnOutput(this, "DataRequestsTable", {
            value: this.dataRequestsTable.tableName,
        });

        this.trainingTable = new dynamodb.Table(this, "TrainingJobs", {
            partitionKey: {name: "Id", type: dynamodb.AttributeType.STRING},
            billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
            encryption: TableEncryption.AWS_MANAGED,
            removalPolicy: RemovalPolicy.DESTROY
        });

        this.trainingTable.addGlobalSecondaryIndex({
            indexName: "LabellingJobArn-index",
            partitionKey: {
                name: "LabellingJobArn",
                type: dynamodb.AttributeType.STRING,
            },
        });

        new core.CfnOutput(this, "TrainingJobsTable", {
            value: this.trainingTable.tableName,
        });

        this.verificationHistoryTable = new dynamodb.Table(
            this,
            "VerificationHistory",
            {
                partitionKey: {
                    name: "SessionId",
                    type: dynamodb.AttributeType.STRING,
                },
                billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
                encryption: TableEncryption.AWS_MANAGED,
            }
        );

        new core.CfnOutput(this, "VerificationHistoryTable", {
            value: this.verificationHistoryTable.tableName,
        });
    }
}
