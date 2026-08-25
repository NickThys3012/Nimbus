import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { Logo } from '../../../components/logo/logo';
import { Register } from './components/register/register';
import { Login } from './components/login/login';

@Component({
  selector: 'Nimbus-login-register',
  imports: [Logo, Register, Login],
  templateUrl: './login-register.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './login-register.css',
})
export default class LoginRegister {
  activeTab = signal<string>('login');
  showLogin = computed(() => this.activeTab() === 'login');
}
