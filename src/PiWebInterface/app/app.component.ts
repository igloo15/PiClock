import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { interval, Observable } from 'rxjs';
import { startWith, switchMap, map } from 'rxjs/operators';
import { PiTimer } from './components/pi.timer/pi.timer';

interface PiStatus {
    temp: number;
    humidity: number;
    heatIndex: number;
    dewPoint: number;
    errorsSinceLastUpdate: number;
}


@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
    title = 'pi-clock';

    time = new Date();
    status: PiStatus;
    counters: PiTimer[] = [];

    private counter = 0;

    constructor(private http: HttpClient) {
        this.status = { temp: 0, humidity: 0, dewPoint: 0, heatIndex: 0, errorsSinceLastUpdate: 0 };
        this.counters.push(new PiTimer('Work Time'));
        this.counters.push(new PiTimer('Personal Time'));
    }

    public ngOnInit(): void {

        interval(100)
        .pipe(
            startWith(0),
            map(() => new Date())
        )
        .subscribe(res => this.time = res);

        interval(5000)
        .pipe(
            startWith(0),
            switchMap(() => {
                try {
                    if (this.counter > 2880) {
                        this.reload();
                        this.counter = 0;
                    } else {
                        this.counter++;
                    }

                    return this.getStatus();
                }
                catch (error) {
                    console.error(error);
                    return new Observable<PiStatus>(subscriber => {
                        subscriber.next(this.status);
                        subscriber.complete();
                    });
                }
            })
        )
        .subscribe(res => { this.status = res; });

    }

    private getStatus(): Observable<PiStatus> {
        return this.http.get<PiStatus>('http://localhost:5000/api/pistatus')
                .pipe(
                    map(res => res as PiStatus)
                );
    }

    public reload(): void {
        console.log('reloading');
        this.counters.forEach(item => {
            item.save();
        });
        window.location.reload(true);
    }
}
