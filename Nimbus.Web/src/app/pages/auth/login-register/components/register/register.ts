import { Component } from '@angular/core';
import { Button } from '../../../../../components/button/button';
import { Banner } from '../../../../../components/banner/banner';
import { InputField } from '../../../../../components/form/input-field/input-field';
import { PasswordField } from '../../../../../components/form/password-field/password-field';

@Component({
  selector: 'Nimbus-register',
  imports: [Button, Banner, InputField, PasswordField],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register {}
