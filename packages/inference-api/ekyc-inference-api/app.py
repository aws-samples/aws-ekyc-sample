import os

import cv2
from PIL import Image
from flask import jsonify, Flask, render_template, request
from pytesseract import pytesseract
from werkzeug.utils import secure_filename

from thai_id import extract_thai_id_front_info, extract_thai_id_back_info, card_to_dict
from util import is_running_in_lambda

os.environ["TESSDATA_PREFIX"] = os.path.abspath("./static/tessdata")


def get_upload_folder():
    if is_running_in_lambda():
        return '/tmp/uploads'
    else:
        return './static/uploads'


app = Flask(__name__)
app.config['UPLOAD_FOLDER'] = get_upload_folder()
app.config['MAX_CONTENT_LENGTH'] = 10 * 1024 * 1024


@app.route("/healthcheck", methods=['GET'])
def healthcheck():
    return jsonify({"msg": "OK"})


@app.route("/")
def index():
    return render_template("index.html")


@app.route('/thai', methods=['POST'])
def detect_thai_text():
    f = request.files['file']
    filename = secure_filename(f.filename)

    # save file to /static/uploads
    filepath = os.path.join(app.config['UPLOAD_FOLDER'], filename)
    f.save(filepath)
    image = cv2.imread(filepath)
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # apply thresholding to preprocess the image
    gray = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)[1]

    # apply median blurring to remove any blurring
    gray = cv2.medianBlur(gray, 3)

    # save the processed image in the /static/uploads directory
    ofilename = os.path.join(app.config['UPLOAD_FOLDER'], "{}.png".format(os.getpid()))
    cv2.imwrite(ofilename, gray)

    # perform OCR on the processed image
    text = pytesseract.image_to_string(Image.open(ofilename), lang="th", timeout=10)
    text = text.replace(" ", "")
    text = text.replace("\n", "")
    print(f"Text output: {text}")

    # remove the processed image - keep for debugging
    os.remove(ofilename)

    return {"result": text}


@app.route('/thai/id/front', methods=['GET', 'POST'])
def detect_thai_id_front():
    if request.method == 'POST':
        f = request.files['file']
        lang = request.args.get("lang")

        if lang is None:
            lang = "th"

        # create a secure filename
        filename = secure_filename(f.filename)

        # save file to /static/uploads
        filepath = os.path.join(app.config['UPLOAD_FOLDER'], filename)
        f.save(filepath)

        # load the example image and convert it to grayscale
        image = cv2.imread(filepath)

        ofilename = os.path.join(app.config['UPLOAD_FOLDER'], "{}.png".format(os.getpid()))
        cv2.imwrite(ofilename, image)

        response = extract_thai_id_front_info(ofilename)

        return jsonify(card_to_dict(response))


@app.route('/thai/id/back', methods=['POST'])
def detect_thai_id_back():
    if request.method == 'POST':
        f = request.files['file']
        lang = request.args.get("lang")

        if lang is None:
            lang = "th"

        # create a secure filename
        filename = secure_filename(f.filename)

        # save file to /static/uploads
        filepath = os.path.join(app.config['UPLOAD_FOLDER'], filename)
        f.save(filepath)

        # load the example image and convert it to grayscale
        image = cv2.imread(filepath)

        ofilename = os.path.join(app.config['UPLOAD_FOLDER'], "{}.png".format(os.getpid()))
        cv2.imwrite(ofilename, image)

        response = extract_thai_id_back_info(ofilename)
        print(response)

        return jsonify(card_to_dict(response))


if __name__ == '__main__':
    port = int(os.environ.get('PORT', 8000))
    app.run(debug=True, host='0.0.0.0', port=port)
    # app.run(host="0.0.0.0", port=5000, debug=True)
