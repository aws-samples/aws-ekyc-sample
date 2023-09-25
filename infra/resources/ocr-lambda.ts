import {Construct} from "constructs";
import {Architecture, DockerImageCode, DockerImageFunction, IFunction} from "aws-cdk-lib/aws-lambda";
import {Platform} from "aws-cdk-lib/aws-ecr-assets";
import * as path from "path";
import {Bucket} from "aws-cdk-lib/aws-s3";

export interface OcrLambdaProps {
    storageBucket: Bucket
}

export class OcrLambda extends Construct {
    readonly inferenceLambda: IFunction

    constructor(scope: Construct, id: string, props: OcrLambdaProps) {
        super(scope, id);

        const {storageBucket} = props


        this.inferenceLambda = new DockerImageFunction(this, `inference-lambda`, {
            code: DockerImageCode.fromImageAsset(path.join(__dirname, "../../packages/inference-api/ekyc-inference-api"), {
                file: "lambda.Dockerfile",
                platform: Platform.LINUX_AMD64,
            }),
            architecture: Architecture.X86_64,
            environment: {
                StorageBucket: storageBucket.bucketName
            }
        })

        if (this.inferenceLambda.role) {
            storageBucket.grantReadWrite(this.inferenceLambda.role)
        }
    }
}