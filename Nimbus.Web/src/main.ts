import { bootstrapApplication } from '@angular/platform-browser';
import { nimbusConfig } from './app/Nimbus.config';
import { Nimbus } from './app/Nimbus';

bootstrapApplication(Nimbus, nimbusConfig).catch((err) => console.error(err));
