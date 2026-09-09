import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Logo } from '../../../components/logo/logo';
import { Register } from './components/register/register';
import { Login } from './components/login/login';

type AuthTab = 'login' | 'register';

@Component({
  selector: 'Nimbus-login-register',
  imports: [Logo, Register, Login],
  templateUrl: './login-register.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './login-register.css',
})
export default class LoginRegister implements OnInit {
  activeTab = signal<AuthTab>('login');
  showLogin = computed(() => this.activeTab() === 'login');
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      this.activeTab.set(this.getTabFromRoute(params.get('tab')));
    });
  }

  selectTab(tab: AuthTab): void {
    if (this.activeTab() === tab) {
      return;
    }

    this.activeTab.set(tab);
    void this.router.navigate(tab === 'login' ? ['/login'] : ['/login', 'register']);
  }

  private getTabFromRoute(tab: string | null): AuthTab {
    return tab === 'register' ? 'register' : 'login';
  }
}
