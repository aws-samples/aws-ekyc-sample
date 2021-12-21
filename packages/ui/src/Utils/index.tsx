import {API} from "aws-amplify";
import axios from "axios";


class Utils {

    static uploadFile = async (url:string,toUpload:string,contentType:string)=>{
        console.log(toUpload)
        const strippedData = await Utils.stripBase64Chars(toUpload)
        const buff = new Buffer(strippedData, 'base64');
        await axios.put(url,buff, {headers:{'Content-Type':contentType}})
    }

    static uploadFileBytes = async (url:string,toUpload:Uint8Array,contentType:string)=>{
        console.log(toUpload)

        await axios.put(url,toUpload, {headers:{'Content-Type':contentType}})
    }

    static stripBase64Chars = async (data : string) => {

        return data.replace(/^data:image\/[a-z]+;base64,/, "")

    }

    static getDateString = (epochs:number) => {
       const d = new Date(epochs)
        return d.getDate() + '/' + (d.getMonth()+1) + '/' + d.getFullYear()
    }

    static getDateTimeString = (epochs:number) => {
        const d = new Date(epochs)
         return d.getDate() + '/' + (d.getMonth()+1) + '/' + d.getFullYear() + " " + d.getHours() + ":" + d.getMinutes() + ":" + d.getSeconds()
     }
     
     static getDateTimeStringFromString = (dt:string) => {
        const d = new Date(dt)
         return d.getDate() + '/' + (d.getMonth()+1) + '/' + d.getFullYear() + " " + d.getHours() + ":" + d.getMinutes() + ":" + d.getSeconds()
     }

}

export default Utils
