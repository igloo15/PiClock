import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { interval, Observable } from 'rxjs';
import { startWith, switchMap, map } from 'rxjs/operators';
import { PiTimer } from './components/pi.timer/pi.timer';
import { PiCommand, PiStatus } from './models/pi.status';
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
    lastUpdateTime = 0;

    private counter = 0;

    constructor(private http: HttpClient) {
        this.status =
            { temp: 0, humidity: 0, dewPoint: 0, heatIndex: 0, errorsSinceLastUpdate: 0, timeSinceLastUpdate: 0, time: 0, clocks: [] };
        this.lastUpdateTime = this.time.getTime();
        this.counters.push(new PiTimer('work', 'Work Time', http));
        this.counters.push(new PiTimer('personal', 'Personal Time', http));
    }

    public ngOnInit(): void {

        interval(100)
        .pipe(
            startWith(0),
            map(() => new Date())
        )
        .subscribe(res => this.time = res);

        this.startStatusLoop();
        this.startCommandLoop();
    }

    private startCommandLoop(): void {
        interval(1000)
        .pipe(
            startWith(0),
            switchMap(() => {
                try {
                    return this.getCommands();
                }
                catch (error) {
                    console.error(error);
                }
            })
        )
        .subscribe(res => this.processCommands(res));
    }


    private startStatusLoop(): void {
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
        .subscribe(res => {
            this.status = res;
            this.lastUpdateTime = (this.time.getTime() / 1000) - this.status.time;
        });
    }

    private getCommands(): Observable<PiCommand[]> {
        return this.http.get<PiCommand[]>('http://localhost:5000/api/pistatus/commands')
                .pipe(
                    map(res => res as PiCommand[])
                );
    }

    private getStatus(): Observable<PiStatus> {
        return this.http.get<PiStatus>('http://localhost:5000/api/pistatus')
                .pipe(
                    map(res => res as PiStatus)
                );
    }

    private processCommands(commands: PiCommand[]): void {

        for (const command of commands) {
            const foundClock = this.counters.find(pt => pt.name === command.name);
            if (foundClock) {
                const action = command.data.action;
                switch (action) {
                    case 'start':
                        foundClock.start();
                        break;
                    case 'stop':
                        foundClock.stop();
                        break;
                    case 'save':
                        foundClock.save();
                        break;
                    case 'reset':
                        foundClock.reset();
                        break;
                    case 'add':
                        foundClock.addTime(command.data.time);
                        break;
                    case 'remove':
                        foundClock.removeTime(command.data.time);
                        break;
                    case 'reload':
                        this.reload();
                        break;
                }
                if (command.data.action === 'start') {
                    foundClock.start();
                } else if (command.name === 'stop') {
                    foundClock.stop();
                }
            }
        }
    }

    public reload(): void {
        console.log('reloading');
        this.counters.forEach(item => {
            item.save();
        });
        window.location.reload(true);
    }
}
