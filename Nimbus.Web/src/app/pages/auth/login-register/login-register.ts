import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { Logo } from '../../../components/logo/logo';
import { Register } from './components/register/register';
import { Banner } from '../../../components/banner/banner';
import { Button } from '../../../components/button/button';
import { PasswordField } from '../../../components/form/password-field/password-field';
import { InputField } from '../../../components/form/input-field/input-field';

@Component({
  selector: 'Nimbus-login-register',
  imports: [Logo, Register, Banner, Button, PasswordField, InputField],
  templateUrl: './login-register.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './login-register.css',
})
export default class LoginRegister {
  activeTab = signal<string>('login');
  showLogin = computed(() => this.activeTab() === 'login');
}
