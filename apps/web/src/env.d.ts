/// <reference path="../.astro/types.d.ts" />

declare namespace App {
  interface Locals {
    userId: string;
    email: string;
    displayName: string;
    avatarUrl: string;
    isEmailVerified: boolean;
  }
}
