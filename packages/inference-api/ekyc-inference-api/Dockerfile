FROM ubuntu:20.04
EXPOSE 8000
ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
  && apt-get -y install tesseract-ocr \
  && apt-get install -y python3 python3-distutils python3-pip libdmtx0b \
  && cd /usr/local/bin \
  && ln -s /usr/bin/python3 python \
  && pip3 --no-cache-dir install --upgrade pip \
  && rm -rf /var/lib/apt/lists/*

RUN apt update \
  && apt-get install ffmpeg libsm6 libxext6 tesseract-ocr libtesseract-dev -y
RUN pip3 install pytesseract
RUN pip3 install opencv-python
RUN pip3 install pillow
RUN pip3 install pylibdmtx

COPY . /app
WORKDIR /app

RUN pip install -r requirements.txt

ENTRYPOINT ["python3"]
CMD ["app.py"]