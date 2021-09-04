import { HttpClient } from '@angular/common/http';
import { interval, Observable } from 'rxjs';
import { startWith, switchMap, map } from 'rxjs/operators';
import { PiClockStatus, PiStatus } from '../../models/pi.status';

export class PiTimer {
    counter = 0;
    timeout: any;
    actionText = 'Start';
    name: string;
    displayName: string;
    status: string;
    http: HttpClient;

    constructor(name: string, displayName: string, http: HttpClient) {
        this.name = name;
        this.displayName = displayName;
        this.http = http;
        this.load();
        if (this.actionText === 'Start') {
            this.stop();
        } else {
            this.start();
        }
    }

    get totalSeconds(): number {
        return this.counter;
    }

    get secondString(): string {
        return (this.totalSeconds % 60).toString().padStart(2, '0');
    }

    get totalMinutes(): number {
        return Math.floor(this.totalSeconds / 60);
    }

    get minuteString(): string {
        return (this.totalMinutes % 60).toString().padStart(2, '0');
    }

    get totalHours(): number {
        return Math.floor(this.totalMinutes / 60);
    }

    get hourString(): string {
        return this.totalHours.toString().padStart(2, '0');
    }

    get timeString(): string {
        return `${this.hourString}:${this.minuteString}:${this.secondString}`;
    }

    toggle(): void {
        if (this.actionText === 'Stop') {
            this.stop();
        } else {
            this.start();
        }
    }

    start(): void {
        if (this.status === 'running') {
            return;
        }
        this.actionText = 'Stop';
        this.status = 'running';
        this.updateClock();
        this.timeout = setInterval(() => {
            if (this.counter % 60 === 0) {
                this.save();
            }
            this.counter++;
        }, 1000);
    }

    stop(): void {
        this.actionText = 'Start';
        this.status = 'stopped';
        this.updateClock();
        if (this.timeout) {
            clearInterval(this.timeout);
            this.save();
        }
    }

    reset(): void {
        this.stop();
        this.counter = 0;
        window.localStorage.setItem(`${this.name}-timer`, '0');
    }

    load(): void {
        const lastCounter = window.localStorage.getItem(`${this.name}-timer`);
        if (lastCounter) {
            this.counter = +lastCounter;
        }
        const actionText = window.localStorage.getItem(`${this.name}-action`);
        if (actionText) {
            this.actionText = actionText;
        }
    }

    save(): void {
        window.localStorage.setItem(`${this.name}-timer`, this.counter.toString());
        window.localStorage.setItem(`${this.name}-action`, this.actionText);
        this.updateClock();
    }

    addTime(seconds: number): void {
        this.counter += seconds;
    }

    removeTime(seconds: number): void {
        this.counter -= seconds;
    }

    private updateClock(): void {
        const status = {
            name: this.name,
            displayName: this.displayName,
            status: this.status,
            timeInSeconds: this.counter
        };
        this.http.post<PiClockStatus>('http://localhost:5000/api/pistatus', status).subscribe(res => {});
    }
}
