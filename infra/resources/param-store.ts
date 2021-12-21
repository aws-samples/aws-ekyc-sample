import * as cdk from "@aws-cdk/core";
import {StringParameter} from "@aws-cdk/aws-ssm";

interface ParamStoreProps extends cdk.StackProps {

}


export class ParamStoreConstruct extends cdk.Construct {

    readonly rekognitionCustomLabelsProjectArn: StringParameter

    readonly rekognitionCustomLabelsProjectVersionArn: StringParameter

    readonly useFieldCoordinatesExtractionMethod : StringParameter

    constructor(scope: cdk.Construct, id: string, props: ParamStoreProps) {
        super(scope, id);

        // This value gets filled after training is complete
        this.rekognitionCustomLabelsProjectArn = new StringParameter(this, 'ekyc-rekognition-arn', {
            stringValue: 'default',

        })

        new cdk.CfnOutput(this, "rekognition-custom-labels-arn", {
            value: this.rekognitionCustomLabelsProjectArn.parameterName,
            description: "Rekognition custom labels project ARN parameter name",
            exportName: "rekognition-custom-labels-arn",
        });


        // This value gets filled after training is complete
        this.rekognitionCustomLabelsProjectVersionArn = new StringParameter(this, 'ekyc-rekognition-version-arn', {
            stringValue: 'default',

        })

        new cdk.CfnOutput(this, "rekognition-custom-labels-project-arn", {
            value: this.rekognitionCustomLabelsProjectVersionArn.parameterName,
            description: "Rekognition custom labels project version ARN parameter name",
            exportName: "rekognition-custom-labels-version-arn",
        });

       
         // This stores whether the field coordinate extraction method is used on documents by default, or else Textract is used
         this.useFieldCoordinatesExtractionMethod = new StringParameter(this, 'ekyc-field-extraction-method', {
            stringValue: 'false',

        })

        new cdk.CfnOutput(this, "field-extraction-method", {
            value: this.useFieldCoordinatesExtractionMethod.parameterName,
            description: "Use the field coordinates method to extract field data from documents. Otherwise, Textract is used.",
            exportName: "field-extraction-method",
        });

    }

}
