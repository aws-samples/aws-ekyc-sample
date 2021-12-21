FROM public.ecr.aws/lambda/dotnet:core3.1

RUN yum update -y

RUN yum install -y \
        python3 \
        python3-pip \
        python3-setuptools \
        groff \
        less \
    && pip3 install --upgrade pip

RUN yum install -y amazon-linux-extras 

RUN amazon-linux-extras install epel -y

RUN pip3 --no-cache-dir install --upgrade awscli

WORKDIR /var/task

COPY "bin/Debug/netcoreapp3.1/*" ${LAMBDA_TASK_ROOT}

CMD [ "ekyc-api::ekyc_api.LambdaEntryPoint::FunctionHandlerAsync" ]