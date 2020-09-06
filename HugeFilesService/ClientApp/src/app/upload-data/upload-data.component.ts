// See example here: https://stackoverflow.com/questions/60182054/upload-large-files-using-angular-8-and-net-core-web-api
import { Component } from '@angular/core';
import { HttpClient, HttpRequest, HttpEventType, HttpResponse } from '@angular/common/http';

@Component({
  selector: 'app-upload-data',
  templateUrl: './upload-data.component.html',
})
export class UploadDataComponent {
  public progress: number;
  public message: string;
  constructor(private http: HttpClient) { }

  upload(files) {
    if (files.length === 0) {
      return;
    }

    const formData = new FormData();

    for (const file of files) {
      formData.append(file.name, file);
    }

    const uploadReq = new HttpRequest('POST', `api/UploadData`, formData, {
      reportProgress: true,
    });

    this.http.request(uploadReq).subscribe(event => {
      if (event.type === HttpEventType.UploadProgress) {
        this.progress = Math.round(100 * event.loaded / event.total);
      } else if (event.type === HttpEventType.Response) {
        this.message = event.body.toString();
      }
    });
  }
}
