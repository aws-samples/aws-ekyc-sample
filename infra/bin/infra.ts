#!/usr/bin/env node
import "source-map-support/register";
import * as cdk from "@aws-cdk/core";
import {EkycInfraStack} from "../lib/infra-stack";

const app = new cdk.App();


const ekycStack = new EkycInfraStack(app, "EkycInfraStack", {});


