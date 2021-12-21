#!/bin/sh

#aws textract analyze-id --document-pages "[{\"S3Object\":{\"Bucket\":\"${1}\",\"Name\":\"0c4ec983-5d8b-4b5b-8cba-f16faa292aff/sgpassport.jpg\"}}]" --region ap-southeast-1

/opt/awscli/aws textract analyze-id --document-pages "[{\"S3Object\":{\"Bucket\":\"${1}\",\"Name\":\"${2}\"}}]" --region ap-southeast-1