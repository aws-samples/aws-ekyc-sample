import {StringParameter} from "aws-cdk-lib/aws-ssm";
import {Construct} from "constructs";
import {CfnOutput} from "aws-cdk-lib";


export class ParamStoreConstruct extends Construct {

    readonly rekognitionCustomLabelsProjectArn: StringParameter

    readonly rekognitionCustomLabelsProjectVersionArn: StringParameter

    readonly useFieldCoordinatesExtractionMethod : StringParameter

    constructor(scope: Construct, id: string) {
        super(scope, id);

        // This value gets filled after training is complete
        this.rekognitionCustomLabelsProjectArn = new StringParameter(this, 'ekyc-rekognition-arn', {
            stringValue: 'default',

        })

        new CfnOutput(this, "rekognition-custom-labels-arn", {
            value: this.rekognitionCustomLabelsProjectArn.parameterName,
            description: "Rekognition custom labels project ARN parameter name",
            exportName: "rekognition-custom-labels-arn",
        });


        // This value gets filled after training is complete
        this.rekognitionCustomLabelsProjectVersionArn = new StringParameter(this, 'ekyc-rekognition-version-arn', {
            stringValue: 'default',

        })

        new CfnOutput(this, "rekognition-custom-labels-project-arn", {
            value: this.rekognitionCustomLabelsProjectVersionArn.parameterName,
            description: "Rekognition custom labels project version ARN parameter name",
            exportName: "rekognition-custom-labels-version-arn",
        });


         // This stores whether the field coordinate extraction method is used on documents by default, or else Textract is used
         this.useFieldCoordinatesExtractionMethod = new StringParameter(this, 'ekyc-field-extraction-method', {
            stringValue: 'false',

        })

        new CfnOutput(this, "field-extraction-method", {
            value: this.useFieldCoordinatesExtractionMethod.parameterName,
            description: "Use the field coordinates method to extract field data from documents. Otherwise, Textract is used.",
            exportName: "field-extraction-method",
        });

    }

}
