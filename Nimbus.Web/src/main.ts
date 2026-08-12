import { bootstrapApplication } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { nimbusConfig, reportPageViews } from './app/Nimbus.config';
import { Nimbus } from './app/Nimbus';
import { TelemetryService } from './app/core/telemetry/telemetry.service';

bootstrapApplication(Nimbus, nimbusConfig)
  .then((appRef) => {
    const injector = appRef.injector;
    reportPageViews(injector.get(Router), injector.get(TelemetryService));
  })
  .catch((err) => console.error(err));
