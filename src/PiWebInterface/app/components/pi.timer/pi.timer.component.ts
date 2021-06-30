import { Component, Input, OnInit } from '@angular/core';
import { PiTimer } from './pi.timer';

@Component({
    selector: 'app-pi-timer',
    templateUrl: './pi.timer.component.html',
    styleUrls: ['./pi.timer.component.scss']
})
export class PiTimerComponent implements OnInit {

    @Input() timer: PiTimer;

    constructor() { }

    ngOnInit(): void {
    }

}
