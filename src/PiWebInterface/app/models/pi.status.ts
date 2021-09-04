
export interface PiClockStatus {
    name: string;
    displayName: string;
    status: string;
    timeInSeconds: number;
}

export interface PiCommand {
    name: string;
    data: any;
}

export interface PiStatus {
    temp: number;
    humidity: number;
    heatIndex: number;
    dewPoint: number;
    errorsSinceLastUpdate: number;
    time: number;
    timeSinceLastUpdate: number;
    clocks: PiClockStatus[];
}



